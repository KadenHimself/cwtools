namespace CWTools.Games
open CWTools.Common
open CWTools.Validation
open CWTools.Validation.ValidationCore
open CWTools.Utilities.Utils
open CWTools.Rules
open CWTools.Process
open CWTools.Parser.Types
open CWTools.Validation.Stellaris.STLLocalisationValidation
open CWTools.Utilities.Position
open CWTools.Utilities.TryParser
open CWTools.Process.Scopes
open FSharp.Collections.ParallelSeq
open CWTools.Process.Localisation

type LookupFileValidator<'T when 'T :> ComputedData> = Files.FileManager -> RuleValidationService option -> Lookup -> FileValidator<'T>

type ValidationManagerSettings<'T when 'T :> ComputedData> = {
    validators : (StructureValidator<'T> * string) list
    experimentalValidators : (StructureValidator<'T> * string) list
    heavyExperimentalValidators : (LookupValidator<'T> * string) list
    experimental : bool
    fileValidators : (FileValidator<'T> * string) list
    lookupValidators : (LookupValidator<'T> * string) list
    lookupFileValidators : (LookupFileValidator<'T> * string) list
    useRules : bool
    debugRulesOnly : bool
    localisationValidators : LocalisationValidator<'T> list
}

type ValidationManagerServices<'T when 'T :> ComputedData> = {
    resources : IResourceAPI<'T>
    lookup : Lookup
    ruleValidationService : RuleValidationService option
    infoService : InfoService option
    localisationKeys : unit -> (Lang * Set<string>) list
    fileManager : Files.FileManager
}
type ValidationManager<'T when 'T :> ComputedData>
        (settings : ValidationManagerSettings<'T>
        , services : ValidationManagerServices<'T>,
         validateLocalisationCommand,
         defaultContext : ScopeContext,
         noneContext : ScopeContext,
         errorCache : System.Collections.Concurrent.ConcurrentDictionary<_, CWError list>) =
    let resources = services.resources
    let validators = settings.validators
    let errorCache = errorCache
    let addToCache (entity : Entity) errors =
        let cache = (errorCache :> System.Collections.Generic.IDictionary<_,_>)
        if cache.ContainsKey entity then cache.[entity] <- errors else cache.Add(entity, errors)
    let getErrorsForEntity (entity : Entity) =
        tryParseWith (errorCache.TryGetValue) entity
    let validate (shallow : bool) (entities : struct (Entity * Lazy<'T>) list) =
        log (sprintf "Validating %i files" (entities.Length))
        let oldEntities = EntitySet (resources.AllEntities())
        let newEntities = EntitySet entities
        let runValidators f (validators : (StructureValidator<'T> * string) list) =
            (validators <&!!&> (fun (v, s) -> duration (fun _ -> f v) s) |> (function Invalid (_ , es) -> es |_ -> []))
            @ (if not settings.experimental then [] else settings.experimentalValidators <&!&> (fun (v, s) -> duration (fun _ -> f v) s) |> (function Invalid (_ , es) -> es |_ -> []))
        // log "Validating misc"
        let res = runValidators (fun f -> f oldEntities newEntities) validators
        // log "Validating rules"
        // let rres = (if settings.useRules && services.ruleValidationService.IsSome then (runValidators (fun f -> f oldEntities newEntities) [services.ruleValidationService.Value.RuleValidate(), "rules"]) else [])
        let ruleValidate =
            (fun (e : Entity) ->
                let res = services.ruleValidationService.Value.RuleValidateEntity e
                let errors = res |> (function | Invalid (_, es) -> es | _ -> [])
                addToCache e errors
                res)
        let rres =
            if settings.useRules && services.ruleValidationService.IsSome
            then
                entities |> List.map (fun struct (e, _) -> e) <&!!&> ruleValidate |> (function | Invalid (_, es) -> es | _ -> [])
            else
                []
        let rres = rres |> List.filter (fun err -> err.code <> "CW100")
        let shallow, deep =
            if settings.debugRulesOnly
            then rres, []
            else

            // log "Validating files"
            let fres = settings.fileValidators <&!&> (fun (v, s) -> duration (fun _ -> v resources newEntities) s) |> (function Invalid (_ , es) -> es |_ -> [])
            // log "Validating effects/triggers"
            let lres = settings.lookupValidators <&!&> (fun (v, s) -> duration (fun _ -> v services.lookup oldEntities newEntities) s) |> function Invalid (_ , es) -> es |_ -> []
            let lfres = settings.lookupFileValidators <&!&> (fun (v, s) -> duration (fun _ -> v services.fileManager services.ruleValidationService services.lookup resources newEntities) s) |> function Invalid (_ , es) -> es |_ -> []
            let hres = if settings.experimental && (not (shallow)) then settings.heavyExperimentalValidators <&!&> (fun (v, s) -> duration (fun _ -> v services.lookup oldEntities newEntities) s) |> function Invalid (_ , es) -> es |_ -> [] else []
            res @ fres @ lres @ lfres @ rres, hres
        shallow, deep

    let validateLocalisation (entities : struct (Entity * Lazy<'T>) list) =
        log (sprintf "Localisation check %i files" (entities.Length))
        let timer = System.Diagnostics.Stopwatch()
        timer.Start()
        let oldEntities = EntitySet (resources.AllEntities())
        let newEntities = EntitySet entities
        let vs = (settings.localisationValidators |> List.map (fun v -> v oldEntities (services.localisationKeys()) newEntities) |> List.fold (<&&>) OK)
        let typeVs =
            if settings.useRules && services.infoService.IsSome
            then
                (entities |> List.map (fun struct (e, _) -> e) |> PSeq.map (services.infoService.Value.GetTypeLocalisationErrors)) |> Seq.fold (<&&>) OK
            else OK
        let vs = if settings.debugRulesOnly then typeVs else vs <&&> typeVs
        log (sprintf "Localisation check took %ims" timer.ElapsedMilliseconds)
        log (sprintf "%A" vs)
        ((vs) |> (function Invalid (_ , es) -> es |_ -> []))

    let createScopeContextFromReplace (rep : ReplaceScopes option) =
        match rep with
        | None -> noneContext
        | Some rs ->
            let ctx = defaultContext
            let prevctx =
                match rs.prevs with
                | Some prevs -> {ctx with Scopes = prevs}
                | None -> ctx
            let newctx =
                match (rs.this, rs.froms) with
                | Some this, Some froms ->
                    {prevctx with Scopes = this::(prevctx.PopScope); From = froms}
                | Some this, None ->
                    {prevctx with Scopes = this::(prevctx.PopScope)}
                | None, Some froms ->
                    {prevctx with From = froms}
                | None, None ->
                    prevctx
            match rs.root with
            | Some root ->
                {newctx with Root = root}
            | None -> newctx

    let globalTypeDefLoc () =
        let valLocCommand = validateLocalisationCommand services.lookup
        let validateLoc (values : (string * range) list) (locdef : TypeLocalisation)  =
            let res1 (value : string) =
                let validate (locentry : LocEntry) = valLocCommand locentry (createScopeContextFromReplace locdef.replaceScopes)
                services.lookup.proccessedLoc |> List.fold (fun r (_, m) -> Map.tryFind value m |> function | Some le -> validate le <&&> r | None -> r) OK
            //     let value = locdef.prefix + value + locdef.suffix
            //     validateLocalisationCommand (createScopeContextFromReplace locdef.replaceScopes) value
                // let validateLocEntry (locentry : LocEntry<_>) =
                    // locentry.
                // services.lookup.proccessedLoc |> List.fold (fun state (l, keys)  -> state <&&> (Map.tryFind value keys |> Option.map validateLocEntry |> Option.defaultValue OK ) OK
            values
                |> List.filter (fun (s, _) -> s.Contains(".") |> not)
                <&!&> (fun (key, range) ->
                            let fakeLeaf = LeafValue(Value.Bool true, range)
                            let lockey = locdef.prefix + key + locdef.suffix
                            if locdef.explicitField.IsNone then res1 lockey else OK
                            <&&>
                            checkLocKeysLeafOrNode (services.localisationKeys()) lockey fakeLeaf)
        let validateType (typename : string) (values : (string * range) list) =
            match services.lookup.typeDefs |> List.tryFind (fun td -> td.name = typename) with
            |None -> OK
            |Some td ->
                td.localisation |> List.filter (fun locdef -> locdef.required) <&!&> validateLoc values
        let validateSubType (typename : string) (values : (string * range) list) =
            let splittype = typename.Split([|'.'|], 2)
            if splittype.Length > 1
            then
                match services.lookup.typeDefs |> List.tryFind (fun td -> td.name = splittype.[0]) with
                |None -> OK
                |Some td ->
                    match td.subtypes |> List.tryFind (fun st -> st.name = splittype.[1]) with
                    |None -> OK
                    |Some st -> st.localisation |> List.filter (fun locdef -> locdef.required) <&!&> validateLoc values
            else OK
        services.lookup.typeDefInfoForValidation |> Map.toList <&!&> (fun (t, l) -> validateType t l)
        <&&>(services.lookup.typeDefInfoForValidation |> Map.toList <&!&> (fun (t, l) -> validateSubType t l))


    member __.Validate((shallow : bool), (entities : struct (Entity * Lazy<'T>) list))  = validate shallow entities
    member __.ValidateLocalisation(entities : struct (Entity * Lazy<'T>) list) = validateLocalisation entities
    member __.ValidateGlobalLocalisation() = globalTypeDefLoc()
    member __.CachedRuleErrors(entities : struct (Entity * Lazy<'T>) list) =
        let res = entities |> List.map (fun struct (e, l) -> (struct (e, l)), tryParseWith errorCache.TryGetValue e)
        // TODO: This is too performance slow
        // res |> List.filter (fun (e, errors) -> errors.IsNone)
        //             |> List.map fst
        //             |> (validate true)
        //             |> ignore
        let forced = res |> List.filter (fun (e, errors) -> errors.IsNone)
                    |> List.choose (fun (struct (e, _), _) -> tryParseWith errorCache.TryGetValue e)
                    |> List.collect id
        (res |> List.choose (fun (_, errors) -> errors) |> List.collect id) @ forced
    member __.ErrorCache() = errorCache