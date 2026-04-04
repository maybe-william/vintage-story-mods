namespace quickpick

open Vintagestory.API.Common
open Vintagestory.API.Client

module QuickPickLogic =

    type ValidationFailure =
        | WrongItem
        | NullSlot
        | NullEntity
        | NullBlockSelection
        | NullGetToolMode
        | NullGetToolModes
        | NotEntityPlayer
        | CouldNotResolvePlayer
        | NullToolModes
        | ModeOutOfRange of mode:int * length:int
        | WrongMode of string option
        | GetToolModeFailed of string

    type ValidationResult =
        | Valid of IPlayer
        | Invalid of ValidationFailure

    let failureMessage = function
        | WrongItem -> None
        | NullSlot -> Some "Invalid quickpick use: slot or itemstack was null"
        | NullEntity -> Some "Invalid quickpick use: byEntity was null"
        | NullBlockSelection -> Some "Invalid quickpick use: blockSel was null"
        | NullGetToolMode -> Some "Invalid quickpick use: getToolMode delegate was null"
        | NullGetToolModes -> Some "Invalid quickpick use: getToolModes delegate was null"
        | NotEntityPlayer -> Some "Invalid quickpick use: byEntity was not an EntityPlayer"
        | CouldNotResolvePlayer -> Some "Invalid quickpick use: could not resolve player from UID"
        | NullToolModes -> Some "Invalid quickpick use: toolModes was null"
        | ModeOutOfRange (mode, length) ->
            Some $"Invalid quickpick use: mode index {mode} out of range for toolModes length {length}"
        | WrongMode modeCode ->
            let shownCode = defaultArg modeCode "null"
            Some $"Invalid quickpick use: active mode was '{shownCode}', not 'quickpick'"
        | GetToolModeFailed msg ->
            Some ("Invalid quickpick use: GetToolMode invoke failed: " + msg)

    let private tryGetPlayer (byEntity: EntityAgent) =
        match byEntity with
        | :? EntityPlayer as eplr ->
            let byPlayer = byEntity.World.PlayerByUid(eplr.PlayerUID)
            if isNull byPlayer then
                Invalid CouldNotResolvePlayer
            else
                Valid byPlayer
        | _ ->
            Invalid NotEntityPlayer

    let validateQuickPickUse
        (instance: obj)
        (isPropickInstance: obj -> bool)
        (getToolMode: obj -> ItemSlot -> IPlayer -> BlockSelection -> int)
        (getToolModes: obj -> SkillItem array)
        (slot: ItemSlot)
        (byEntity: EntityAgent)
        (blockSel: BlockSelection)
        : ValidationResult =

        if isNull instance then Invalid WrongItem
        elif isNull slot || isNull slot.Itemstack then Invalid NullSlot
        elif isNull byEntity then Invalid NullEntity
        elif isNull blockSel then Invalid NullBlockSelection
        elif obj.ReferenceEquals(box isPropickInstance, null) then Invalid NullGetToolMode
        elif obj.ReferenceEquals(box getToolMode, null) then Invalid NullGetToolMode
        elif obj.ReferenceEquals(box getToolModes, null) then Invalid NullGetToolModes
        elif not (isPropickInstance instance) then Invalid WrongItem
        else
            match tryGetPlayer byEntity with
            | Invalid reason -> Invalid reason
            | Valid byPlayer ->
                try
                    let mode = getToolMode instance slot byPlayer blockSel
                    let modes = getToolModes instance

                    if isNull modes then
                        Invalid NullToolModes
                    elif mode < 0 || mode >= modes.Length then
                        Invalid (ModeOutOfRange (mode, modes.Length))
                    else
                        let modeCode =
                            match modes.[mode] with
                            | null -> None
                            | skill when isNull skill.Code -> None
                            | skill -> Some skill.Code.Path

                        if modeCode <> Some "quickpick" then
                            Invalid (WrongMode modeCode)
                        else
                            Valid byPlayer
                with ex ->
                    Invalid (GetToolModeFailed ex.Message)

    let shouldHandleClientQuickPick firstEvent validationResult side =
        firstEvent
        &&
        match validationResult with
        | Valid _ -> side = EnumAppSide.Client
        | Invalid _ -> false

    let getBlockSelectionPosition (blockSel: BlockSelection) =
        let pos = blockSel.Position
        pos.X, pos.Y, pos.Z