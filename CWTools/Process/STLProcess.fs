namespace CWTools.Process
open CWTools.Process.ProcessCore
open CWTools.Parser
open CWTools.Localisation
open CWTools.Process.ProcessCore
open CWTools.Process
open CWTools.Process.STLScopes
open CWTools.Common.STLConstants
open DotNet.Globbing

module STLProcess =
    let toTriggerBlockKeys = ["limit"; "trigger"; "allow"]
    let _targetKeys = ["THIS"; "ROOT"; "PREV"; "FROM"; "OWNER"; "CONTROLLER"; "CAPITAL"; "SOLAR_SYSTEM"; "LEADER"; "RANDOM"; "FROMFROM"; "FROMFROMFROM"; "FROMFROMFROMFROM"; "PREVPREV"; "PREVPREVPREV"; "PREVPREVPREVPREV";
                        "CAPITAL_SCOPE"]//Added used in STH]
    let targetKeys = _targetKeys |> List.sortByDescending (fun k -> k.Length)
    let toEffectBlockKeys = ["if"; "else"; "tooltip"]
    let ignoreKeys = ["count"; "min_steps"; "max_steps"]


    let rec isTargetKey =
        function
        |"" -> true
        |x ->
            match targetKeys |> List.tryFind (fun f -> x.ToLower().StartsWith(f.ToLower()))  with
            |Some s -> if s.Length = x.Length then true else isTargetKey (x.Substring(s.Length + 1))
            |None -> false

    let rec scriptedTriggerScope (strict : bool) (effects : (string * Scope list) list) (triggers : (string * Scope list) list) (root : string) (node : Node) =
        let targetKeys = ["THIS"; "ROOT"; "PREV"; "FROM"; "OWNER"; "CONTROLLER"; "CAPITAL"; "SOLAR_SYSTEM"; "LEADER"; "RANDOM"; "FROMFROM"; "FROMFROMFROM"; "FROMFROMFROMFROM"; "PREVPREV"; "PREVPREVPREV"; "PREVPREVPREVPREV"]
        let anyBlockKeys = ["OR"; "AND"; "NOR"; "NAND"; "NOT"; "if"; "else"; "hidden_effect"]
        let triggerBlockKeys = ["limit"] //@ targetKeys
        let nodeScopes = node.Children 
                        |> List.map (
                            function
                            | x when x.Key = root -> 
                                allScopes
                            | x when (x.Key.ToLower().StartsWith("event_target:")) ->
                                allScopes
                            | x when targetKeys |> List.exists (fun y -> y.ToLower() = x.Key.ToLower()) ->
                                allScopes
                            | x when anyBlockKeys |> List.exists (fun y -> y.ToLower() = x.Key.ToLower()) ->
                                scriptedTriggerScope strict effects triggers root x
                            | x when triggerBlockKeys |> List.exists (fun y -> y.ToLower() = x.Key.ToLower()) -> 
                                scriptedTriggerScope strict triggers triggers root x
                            | x ->
                                match STLScopes.sourceScope x.Key with
                                | Some v -> v
                                | None -> effects |> List.filter (fun (n, _) -> n = x.Key) |> List.map (fun (_, ss) -> ss) |> List.collect id
                        )
        let valueScopes = node.Values 
                        //|> List.filter (fun v -> v.Key.StartsWith("@"))
                        |> List.map (
                            function
                            | x when x.Key.StartsWith("@") -> allScopes
                            | x when x.Key = root -> allScopes
                            | x -> effects |> List.tryFind (fun (n, _) -> n = x.Key) |> (function |Some (_,s) -> s |None -> [])
                           )
        let combinedScopes = nodeScopes @ valueScopes |> List.map (function | [] -> (if strict then [] else allScopes) |x -> x)
        combinedScopes |> List.fold (fun a b -> Set.intersect (Set.ofList a) (Set.ofList b) |> Set.toList) allScopes
        // let valueTriggers = node.Values |> List.choose (fun v -> if List.contains v.Key anyBlockKeys then None else Some v.Key)
        // //valueTriggers |> List.iter (fun f -> printfn "%A" f)
        // let nodeScopeChanges = node.Children |> List.choose (fun v -> sourceScope v.Key) //|> List.map (fun x -> [x])
        // let nodeSameScope = node.Children |> List.choose (fun v -> match sourceScope v.Key with |Some s -> None |None -> Some v)
        // let nodeTriggers = nodeSameScope |> List.choose (fun v -> if List.contains v.Key anyBlockKeys then None else Some v.Key)
        // let nodeLimit = nodeSameScope |> List.choose (fun v -> if List.contains v.Key triggerBlockKeys then Some v else None)
        // let valueScopes = (valueTriggers @ nodeTriggers)
        //                 |> List.map (fun v -> 
        //                     effects 
        //                     |> List.filter (fun (n, ss) -> n = v)
        //                     |> List.map (fun (n, ss) -> ss)
        //                     |> List.collect id)
        // let nodeRecTriggers = nodeSameScope |> List.choose (fun v -> if List.contains v.Key anyBlockKeys && not (List.contains v.Key triggerBlockKeys) then Some v else None)
        
        // let nodeScopes =
        //     nodeRecTriggers
        //     |> List.map (scriptedTriggerScope effects triggers)
        // let limitScopes = nodeLimit |> List.map (scriptedTriggerScope triggers triggers)
        //nodeScopes @ valueScopes @ nodeScopeChanges @ limitScopes
        //        |> List.fold (fun a b -> Set.intersect (Set.ofList a) (Set.ofList b) |> Set.toList) allScopes

    let getScriptedTriggerScope (firstRun: bool) (effectType : EffectType) (effects : Effect list) (triggers : Effect list) (node : Node) =
        let effects2 = effects |> List.map (fun t -> t.Name, t.Scopes)
        let triggers2 = triggers |> List.map (fun t -> t.Name, t.Scopes)
        let scopes = scriptedTriggerScope firstRun effects2 triggers2 node.Key node
        ScriptedEffect(node.Key, scopes, effectType)

    type Ship (key, pos) =
        inherit Node(key, pos)
        member this.Name = this.TagText "name"
        member this.ShipSize = this.TagText "ship_size"
    
    type ShipSection (key, pos) =
        inherit Node(key, pos)
        member this.Template = this.TagText "template"
        member this.Slot = this.TagText "slot"
    
    type ShipComponent (key, pos) =
        inherit Node(key, pos)
        member this.Template = this.TagText "template"
        member this.Slot = this.TagText "slot"

    type Button_Effect (key, pos) =
        inherit Node(key, pos)

    type Event(key, pos) =
        inherit Node(key, pos)
        member this.ID = this.TagText "id"
        member this.Desc = this.TagText "desc"
        member this.Hidden = this.Tag "hide_window" |> (function | Some (Bool b) -> b | _ -> false)
   
    type EffectBlock(key, pos) = inherit Node(key, pos)
    type TriggerBlock(key, pos) = 
        inherit Node(key, pos)
        member val InEffectBlock : bool = false with get, set
    type Option(key, pos) = inherit Node(key, pos)
    let globCheckPosition (pattern : string) =
        let glob = Glob.Parse(pattern)
        (fun (p : Position) ->
            let p2 = Position.UnConv(p)
            glob.IsMatch(p2.StreamName))

    let scopedProcessNode<'T when 'T :> Node> (lookup : LookupContext) =
        match lookup.scope with
        |"planet" -> processNode<'T> (fun n -> n.Scope <- Scope.Planet; n)
        |"country" -> processNode<'T> (fun n -> n.Scope <- Scope.Country; n)
        |"fleet" -> processNode<'T> (fun n -> n.Scope <- Scope.Fleet; n)
        |"ship" -> processNode<'T> (fun n -> n.Scope <- Scope.Ship; n)
        |"pop_faction" -> processNode<'T> (fun n -> n.Scope <- Scope.PopFaction; n)
        |"pop" -> processNode<'T> (fun n -> n.Scope <- Scope.Pop; n)
        |_ -> processNode<'T> (fun n -> n.Scope <- Scope.Any; n)

    let specificScopeProcessNode<'T when 'T :> Node> (scope : Scope) (lookup : LookupContext) =
        processNode<'T> (fun n -> n.Scope <- scope; n)
    
    // let triggerProcessNode (lookup : LookupContext) =
    //     let postinit = 
    //         match lookup.scope with
    //             |"planet" ->  (fun (n : TriggerBlock) -> n.Scope <- Scope.Planet; n)
    //             |"country" ->  (fun n -> n.Scope <- Scope.Country; n)
    //             |"fleet" ->  (fun n -> n.Scope <- Scope.Fleet; n)
    //             |"ship" ->  (fun n -> n.Scope <- Scope.Ship; n)
    //             |"pop_faction" ->  (fun n -> n.Scope <- Scope.PopFaction; n)
    //             |"pop" ->  (fun n -> n.Scope <- Scope.Pop; n)
    //             |_ ->  (fun n -> n.Scope <- Scope.Any; n)
    //     match lookup.parents with
    //     | "effectblock"::_ -> processNode<TriggerBlock> (postinit >> (fun n -> n.InEffectBlock <- true; n))
    //     | _ -> processNode<TriggerBlock> postinit

    let triggerInEffectProcessNode (lookup : LookupContext) =
        processNode<TriggerBlock> (fun n -> n.InEffectBlock <- true; n)

    
    let shipMap =
        [
            fst3 >> ((=) "ship_design"), processNodeSimple<Ship>, "ship", id;
            fst3 >> ((=) "section"), processNodeSimple<ShipSection>, "shipsection", id;
            fst3 >> ((=) "component"), processNodeSimple<ShipComponent>, "shipcomponent", id;
            fst3 >> ((=) "planet_event"), processNodeSimple<Event>, "event", (fun c -> {c with scope = "planet"});
            fst3 >> ((=) "country_event"), processNodeSimple<Event>, "event", (fun c -> {c with scope = "country"});
            fst3 >> ((=) "fleet_event"), processNodeSimple<Event>, "event", (fun c -> {c with scope = "fleet"});
            fst3 >> ((=) "ship_event"), processNodeSimple<Event>, "event", (fun c -> {c with scope = "ship"});
            fst3 >> ((=) "pop_faction_event"), processNodeSimple<Event>, "event", (fun c -> {c with scope = "pop_faction"});
            fst3 >> ((=) "pop_event"), processNodeSimple<Event>, "event", (fun c -> {c with scope = "pop"});
            fst3 >> ((=) "event"), processNodeSimple<Event>, "event", id;
            //Events
            (function |("trigger", _, {parents = "event"::_}) -> true |_ -> false), scopedProcessNode<TriggerBlock>, "triggerblock", id;
            (function |("immediate", _, { parents = "event"::_ }) -> true |_ -> false), scopedProcessNode<EffectBlock>, "effectblock", id;
            (function |("option", _, {parents = "event"::_}) -> true |_ -> false), scopedProcessNode<Option>, "option", id;
            (function |("tooltip", _, {parents = "option"::_; previous = "option"}) -> true |_ -> false), scopedProcessNode<EffectBlock>, "effectblock", id;
            (function |("allow", _, {parents = "option"::_; previous = "option"}) -> true |_ -> false), scopedProcessNode<TriggerBlock>, "triggerblock", id;
            (function |("trigger", _, {parents = "option"::_; previous = "option"}) -> true |_ -> false), scopedProcessNode<TriggerBlock>, "triggerblock", id;
            (function |("desc", _, {parents = "event"::_}) -> true |_ -> false), scopedProcessNode<Node>, "eventdesc", id;
            (function |("trigger", _, {parents = "eventdesc"::"event"::_}) -> true |_ -> false), scopedProcessNode<TriggerBlock>, "triggerblock", id;
            (function |("after", _, {parents = "event"::_}) -> true |_ -> false), scopedProcessNode<EffectBlock>, "effectblock", id;
            (function |("limit", _, {parents = "effectblock"::_}) -> true |_ -> false), triggerInEffectProcessNode, "triggerblock", id;
            (function |("limit", _, {parents = "option"::_}) -> true |_ -> false), processNodeSimple<TriggerBlock>, "triggerblock", id;
            //Buildings
            (fun (_, p, c) -> (globCheckPosition("**/common/buildings/*.txt") p) && not c.complete), processNodeSimple<Node>, "building",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "building"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Tile, "triggerblock", id;
            (function |("allow", _, {parents = "building"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Tile, "triggerblock", id;
            (function |("ai_allow", _, {parents = "building"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Tile, "triggerblock", id;
            (function |("destroy_if", _, {parents = "building"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Tile, "triggerblock", id;
            (function |("active", _, {parents = "building"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Tile, "triggerblock", id;
            (function |("planet_modifier_with_pop_trigger", _, {parents = "building"::_}) -> true |_ -> false), processNodeSimple<Node>, "planetmodpop", id;
            //Armies
            (fun (_, p, c) -> (globCheckPosition("**/common/armies/*.txt") p) && not c.complete), processNodeSimple<Node>, "army",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "army"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Planet, "triggerblock", id;
            (function |("allow", _, {parents = "army"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Planet, "triggerblock", id;
            (function |("show_tech_unlock_if", _, {parents = "army"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("on_queued", _, {parents = "army"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            (function |("on_unqueued", _, {parents = "army"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            //Anomalies
            (fun (n, p, c) -> n = "anomaly" && (globCheckPosition("**/common/anomalies/*.txt") p) && not c.complete), processNodeSimple<Node>, "anomaly",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "anomaly"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Ship, "triggerblock", id;
            (fun (n, p, c) -> n = "anomaly_category" && (globCheckPosition("**/common/anomalies/*.txt") p) && not c.complete), processNodeSimple<Node>, "anomalycat",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "anomalycat"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Planet, "triggerblock", id;
            (function |("on_spawn", _, {parents = "anomalycat"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Planet, "effectblock", id;
            (function |("on_success", _, {parents = "anomalycat"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Ship, "effectblock", id;
            (function |("on_fail", _, {parents = "anomalycat"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Ship, "effectblock", id;
            (function |("on_critical_fail", _, {parents = "anomalycat"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Ship, "effectblock", id;
            //Ascension perks
            (fun (_, p, c) -> (globCheckPosition("**/common/ascension_perks/*.txt") p) && not c.complete), processNodeSimple<Node>, "ascension",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "ascension"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("possible", _, {parents = "ascension"::_}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("on_enabled", _, {parents = "ascension"::_}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            //Bombardment stances
            (fun (_, p, c) -> (globCheckPosition("**/common/bombardment_stances/*.txt") p) && not c.complete), processNodeSimple<Node>, "bombard",  (fun c -> { c with complete = true});
            (function |("trigger", _, {parents = "bombard"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Fleet, "triggerblock", id;
            //Buildable pops
            (fun (_, p, c) -> (globCheckPosition("**/common/buildable_pops/*.txt") p) && not c.complete), processNodeSimple<Node>, "buildpops",  (fun c -> { c with complete = true});
            (function |("potential_build", _, {parents = "buildpops"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("allow", _, {parents = "buildpops"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("show_tech_unlock_if", _, {parents = "buildpops"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Button effects #todo effects
            (fun (_, p, c) -> (globCheckPosition("**/common/button_effects/*.txt") p) && not c.complete), processNodeSimple<Button_Effect>, "buttoneffect",  (fun c -> { c with complete = true});
            //Bypass
            (fun (_, p, c) -> (globCheckPosition("**/common/bypass/*.txt") p) && not c.complete), processNodeSimple<Node>, "bypass",  (fun c -> { c with complete = true});
            (function |("on_pre_explore", _, {parents = "bypass"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Fleet, "effectblock", id;
            (function |("country_can_use", _, {parents = "bypass"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Casus belli
            (fun (_, p, c) -> (globCheckPosition("**/common/casus_belli/*.txt") p) && not c.complete), processNodeSimple<Node>, "casusbelli",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "casusbelli"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("is_valid", _, {parents = "casusbelli"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("destroy_if", _, {parents = "casusbelli"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Diplomatic actions            
            (fun (_, p, c) -> (globCheckPosition("**/common/diplomatic_actions/*.txt") p) && not c.complete), processNodeSimple<Node>, "diploact",  (fun c -> { c with complete = true});
            (function |("potential", _, {parents = "diploact"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("possible", _, {parents = "diploact"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("proposable", _, {parents = "diploact"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("on_accept", _, {parents = "diploact"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            (function |("on_decline", _, {parents = "diploact"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            //Edicts
            (fun (n, p, c) -> n = "country_edict" && (globCheckPosition("**/common/edicts/*.txt") p) && not c.complete), processNodeSimple<Node>, "edict",  (fun c -> { c with complete = true; scope = "country"});
            (fun (n, p, c) -> n = "planet_edict" && (globCheckPosition("**/common/edicts/*.txt") p) && not c.complete), processNodeSimple<Node>, "edict",  (fun c -> { c with complete = true; scope = "planet"});
            (function |("potential", _, {parents = "edict"::_;}) -> true |_ -> false), scopedProcessNode<TriggerBlock>, "triggerblock", id;
            (function |("allow", _, {parents = "edict"::_;}) -> true |_ -> false), scopedProcessNode<TriggerBlock>, "triggerblock", id;
            (function |("effect", _, {parents = "edict"::_;}) -> true |_ -> false), scopedProcessNode<EffectBlock>, "effectblock", id;
            //Ethics
            (fun (_, p, c) ->  (globCheckPosition("**/common/ethics/*.txt") p) && not c.complete), processNodeSimple<Node>, "ethic",  (fun c -> { c with complete = true;});
            (function |("playable", _, {parents = "ethic"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("pop_attraction_tag", _, {parents = "ethic"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popattractiontag", id;
            (function |("trigger", _, {parents = "popattractiontag"::"ethic"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Mandates
            (fun (_, p, c) ->  (globCheckPosition("**/common/mandates/*.txt") p) && not c.complete), processNodeSimple<Node>, "mandate",  (fun c -> { c with complete = true;});
            (function |("valid", _, {parents = "mandate"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Leader, "triggerblock", id;
            (function |("on_term_started", _, {parents = "mandate"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Leader, "effectblock", id;
            (function |("on_term_ended", _, {parents = "mandate"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Leader, "effectblock", id;
            //Megastrucutres
            (fun (_, p, c) ->  (globCheckPosition("**/common/megastructures/*.txt") p) && not c.complete), processNodeSimple<Node>, "megastructure",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "megastructure"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("possible", _, {parents = "megastructure"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.GalacticObject, "triggerblock", id;
            (function |("placement_rules", _, {parents = "megastructure"::_;}) -> true |_ -> false), processNodeSimple<Node>, "megastructureplacement", id;
            (function |("planet_possible", _, {parents = "megastructureplacement"::"megastructure"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Planet, "triggerblock", id;
            (function |("on_build_start", _, {parents = "megastructure"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.GalacticObject, "effectblock", id;
            (function |("on_build_cancel", _, {parents = "megastructure"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.GalacticObject, "effectblock", id;
            (function |("on_build_complete", _, {parents = "megastructure"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.GalacticObject, "effectblock", id;
            //Observation station missions
            (fun (_, p, c) ->  (globCheckPosition("**/common/observation_station_missions/*.txt") p) && not c.complete), processNodeSimple<Node>, "obsstation",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "obsstation"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("valid", _, {parents = "obsstation"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Personalities
            (fun (_, p, c) ->  (globCheckPosition("**/common/personalities/*.txt") p) && not c.complete), processNodeSimple<Node>, "personality",  (fun c -> { c with complete = true;});
            (function |("allow", _, {parents = "personality"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Policies
            (fun (_, p, c) ->  (globCheckPosition("**/common/policies/*.txt") p) && not c.complete), processNodeSimple<Node>, "policy",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "policy"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("allow", _, {parents = "policy"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("option", _, {parents = "policy"::_;}) -> true |_ -> false), processNodeSimple<Node>, "policyoption", id;
            (function |("valid", _, {parents = "policyoption"::"policy"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "policyoption", id;
            (function |("on_enabled", _, {parents = "policyoption"::"policy"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            (function |("on_disabled", _, {parents = "policyoption"::"policy"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            //Pop faction types
            (fun (_, p, c) ->  (globCheckPosition("**/common/pop_faction_types/*.txt") p) && not c.complete), processNodeSimple<Node>, "popfaction",  (fun c -> { c with complete = true;});
            (function |("is_potential", _, {parents = "popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("can_join_faction", _, {parents = "popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Pop, "triggerblock", id;
            (function |("demand", _, {parents = "popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactiondemand", id;
            (function |("potential", _, {parents = "popfactiondemand"::"popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.PopFaction, "triggerblock", id;
            (function |("trigger", _, {parents = "popfactiondemand"::"popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.PopFaction, "triggerblock", id;
            (function |("on_create", _, {parents = "popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.PopFaction, "effectblock", id;
            (function |("on_destroy", _, {parents = "popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            (function |("actions", _, {parents = "popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactionaction", id;
            (function |("embrace_faction", _, {parents = "popfactionaction"::"popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactionaction2", id;
            (function |("promote_faction", _, {parents = "popfactionaction"::"popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactionaction2", id;
            (function |("cancel_promote_faction", _, {parents = "popfactionaction"::"popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactionaction2", id;
            (function |("suppress_faction", _, {parents = "popfactionaction"::"popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactionaction2", id;
            (function |("cancel_suppress_faction", _, {parents = "popfactionaction"::"popfaction"::_;}) -> true |_ -> false), processNodeSimple<Node>, "popfactionaction2", id;
            (function |("potential", _, {parents = "popfactionaction2"::"popfactionaction"::"popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.PopFaction, "triggerblock", id;
            (function |("effect", _, {parents = "popfactionaction2"::"popfactionaction"::"popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.PopFaction, "effectblock", id;
            (function |("valid", _, {parents = "popfactionaction2"::"popfactionaction"::"popfaction"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.PopFaction, "triggerblock", id;
            //Ship_sizes
            (fun (_, p, c) ->  (globCheckPosition("**/common/ship_sizes/*.txt") p) && not c.complete), processNodeSimple<Node>, "shipsize",  (fun c -> { c with complete = true;});
            (function |("possible_starbase", _, {parents = "shipsize"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Starbase, "triggerblock", id;
            //Solar system
            (fun (_, p, c) ->  (globCheckPosition("**/common/solar_system_initializers/*.txt") p) && not c.complete), processNodeSimple<Node>, "solarsystem",  (fun c -> { c with complete = true;});
            (function |("planet", _, {parents = "solarsystem"::_;}) -> true |_ -> false), processNodeSimple<Node>, "solarplanet", id;
            (function |("init_effect", _, {parents = "solarplanet"::"solarsystem"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Planet, "effectblock", id;
            (function |("neighbor_system", _, {parents = "solarsystem"::_;}) -> true |_ -> false), processNodeSimple<Node>, "solarneighbor", id;
            (function |("trigger", _, {parents = "solarneighbor"::"solarsystem"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.GalacticObject, "triggerblock", id;
            //Special projects
            (fun (_, p, c) ->  (globCheckPosition("**/common/special_projects/*.txt") p) && not c.complete), processNodeSimple<Node>, "specialproject",  (fun c -> { c with complete = true;});
            (function |("on_success", _, {parents = "specialproject"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Any, "effectblock", id;
            ///////TODO special project
            //////
            //Species classes
            (fun (_, p, c) ->  (globCheckPosition("**/common/species_classes/*.txt") p) && not c.complete), processNodeSimple<Node>, "specialclass",  (fun c -> { c with complete = true;});
            //////TODO species classes
            //Species rights
            (fun (_, p, c) ->  (globCheckPosition("**/common/species_rights/*.txt") p) && not c.complete), processNodeSimple<Node>, "speciesrights",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "speciesrights"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Species, "triggerblock", id;
            (function |("allow", _, {parents = "speciesrights"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Species, "triggerblock", id;
            //Starbase buildings
            (fun (_, p, c) ->  (globCheckPosition("**/common/starbase_buildings/*.txt") p) && not c.complete), processNodeSimple<Node>, "starbasebuilding",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "starbasebuilding"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Starbase, "triggerblock", id;
            (function |("possible", _, {parents = "starbasebuilding"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Starbase, "triggerblock", id;
            //Starbase modules
            (fun (_, p, c) ->  (globCheckPosition("**/common/starbase_modules/*.txt") p) && not c.complete), processNodeSimple<Node>, "starbasemodule",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "starbasemodule"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Starbase, "triggerblock", id;
            (function |("possible", _, {parents = "starbasemodule"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Starbase, "triggerblock", id;
            //Starbase type
            (fun (_, p, c) ->  (globCheckPosition("**/common/starbase_types/*.txt") p) && not c.complete), processNodeSimple<Node>, "starbasetype",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "starbasetype"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Starbase, "triggerblock", id;
            //Start screen messages
            (fun (_, p, c) ->  (globCheckPosition("**/common/start_screen_messages/*.txt") p) && not c.complete), processNodeSimple<Node>, "startscreen",  (fun c -> { c with complete = true;});
            (function |("trigger", _, {parents = "startscreen"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Subjects
            (fun (_, p, c) ->  (globCheckPosition("**/common/subjects/*.txt") p) && not c.complete), processNodeSimple<Node>, "subject",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "subject"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("effect", _, {parents = "subject"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            //System types
            (fun (_, p, c) ->  (globCheckPosition("**/common/system_types/*.txt") p) && not c.complete), processNodeSimple<Node>, "systemtype",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "systemtype"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.GalacticObject, "triggerblock", id;
            //Technologies
            (fun (_, p, c) ->  (globCheckPosition("**/common/technology/*.txt") p) && not c.complete), processNodeSimple<Node>, "technology",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "technology"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Terraform
            (fun (_, p, c) ->  (globCheckPosition("**/common/terraform/*.txt") p) && not c.complete), processNodeSimple<Node>, "terraform",  (fun c -> { c with complete = true;});
            (function |("condition", _, {parents = "terraform"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Tradition category
            (fun (_, p, c) ->  (globCheckPosition("**/common/tradition_categories/*.txt") p) && not c.complete), processNodeSimple<Node>, "tradcat",  (fun c -> { c with complete = true;});
            (function |("tradition_swap", _, {parents = "tradcat"::_;}) -> true |_ -> false), processNodeSimple<Node>, "tradcatswap", id;
            (function |("trigger", _, {parents = "tradcatswap"::"tradcat"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Tradition
            (fun (_, p, c) ->  (globCheckPosition("**/common/traditions/*.txt") p) && not c.complete), processNodeSimple<Node>, "trad",  (fun c -> { c with complete = true;});
            (function |("tradition_swap", _, {parents = "trad"::_;}) -> true |_ -> false), processNodeSimple<Node>, "tradswap", id;
            (function |("trigger", _, {parents = "tradswap"::"trad"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            //Traits
            (fun (_, p, c) ->  (globCheckPosition("**/common/traits/*.txt") p) && not c.complete), processNodeSimple<Node>, "trait",  (fun c -> { c with complete = true;});
            (function |("leader_potential_add", _, {parents = "trait"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Leader, "triggerblock", id;
            (function |("species_potential_add", _, {parents = "trait"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Species, "triggerblock", id;
            (function |("species_potential_remove", _, {parents = "trait"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Species, "triggerblock", id;
            //War goals
            (fun (_, p, c) ->  (globCheckPosition("**/common/war_goals/*.txt") p) && not c.complete), processNodeSimple<Node>, "wargoal",  (fun c -> { c with complete = true;});
            (function |("potential", _, {parents = "wargoal"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("possible", _, {parents = "wargoal"::_;}) -> true |_ -> false), specificScopeProcessNode<TriggerBlock> Scope.Country, "triggerblock", id;
            (function |("on_status_quo", _, {parents = "wargoal"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            (function |("on_accept", _, {parents = "wargoal"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            (function |("on_wargoal_set", _, {parents = "wargoal"::_;}) -> true |_ -> false), specificScopeProcessNode<EffectBlock> Scope.Country, "effectblock", id;
            
       ]

    let shipProcess = BaseProcess(shipMap)

    let staticModifierCategory (modifiers : (string * ModifierCategory list) list) (node : Node) =
        node.Values |> List.filter (fun v -> v.Key <> "icon" && v.Key <> "icon_frame")
                    |> List.map (fun v -> List.tryPick (function |(m, c) when m = v.Key -> Some c |_ -> None) modifiers)
                    |> List.choose id
                    |> List.collect id
                    |> List.distinct

    let getStaticModifierCategory (modifiers : Modifier list) (node : Node) =
        let modifiers2 = modifiers |> List.map (fun t -> t.tag, t.categories)
        let category = staticModifierCategory modifiers2 node
        {tag = node.Key; categories = category; core = false}

    
