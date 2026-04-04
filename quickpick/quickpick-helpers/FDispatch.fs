namespace quickpick

open System
open Vintagestory.API.Common
open Vintagestory.API.Client

[<CLIMutable>]
type QuickPickValidationDto =
    { IsValid: bool
      FailureReason: string
      Player: IPlayer }

[<AbstractClass; Sealed>]
type FDispatch private () =

    static member QuickPickValidateUse(
        instance: obj,
        isPropickInstance: Func<obj, bool>,
        getToolMode: Func<obj, ItemSlot, IPlayer, BlockSelection, int>,
        getToolModes: Func<obj, SkillItem[]>,
        slot: ItemSlot,
        byEntity: EntityAgent,
        blockSel: BlockSelection
    ) : QuickPickValidationDto =

        let isPropickInstanceF : (obj -> bool) =
            if isNull isPropickInstance then
                nullArg (nameof isPropickInstance)
            else
                fun instance -> isPropickInstance.Invoke(instance)

        let getToolModeF : (obj -> ItemSlot -> IPlayer -> BlockSelection -> int) =
            if isNull getToolMode then
                nullArg (nameof getToolMode)
            else
                fun instance slot player blockSel ->
                    getToolMode.Invoke(instance, slot, player, blockSel)

        let getToolModesF : (obj -> SkillItem array) =
            if isNull getToolModes then
                nullArg (nameof getToolModes)
            else
                fun instance ->
                    getToolModes.Invoke(instance)

        match
            QuickPickLogic.validateQuickPickUse
                instance
                isPropickInstanceF
                getToolModeF
                getToolModesF
                slot
                byEntity
                blockSel
        with
        | QuickPickLogic.Valid player ->
            { IsValid = true
              FailureReason = null
              Player = player }
        | QuickPickLogic.Invalid reason ->
            { IsValid = false
              FailureReason =
                match QuickPickLogic.failureMessage reason with
                | Some msg -> msg
                | None -> null
              Player = null }

    static member QuickPickShouldHandleClient(
        firstEvent: bool,
        isValidQuickPickUse: bool,
        side: EnumAppSide
    ) : bool =
        let validationResult =
            if isValidQuickPickUse then
                QuickPickLogic.Valid null
            else
                QuickPickLogic.Invalid QuickPickLogic.WrongItem

        QuickPickLogic.shouldHandleClientQuickPick firstEvent validationResult side

    static member QuickPickGetBlockSelectionPosition(
        blockSel: BlockSelection
    ) : ValueTuple<int, int, int> =
        let x, y, z = QuickPickLogic.getBlockSelectionPosition blockSel
        ValueTuple<int, int, int>(x, y, z)