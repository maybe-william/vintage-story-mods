using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using resourcecrates.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace resourcecrates
{
    public static class BlockEntityGenerator
    {
        private static Type? generatedType;

        public static Type GetOrCreateDynamicBlockEntityType(ICoreAPI api)
        {
            if (generatedType != null) return generatedType;

            DebugLogger.Log("BlockEntityGenerator | START dynamic BE generation");

            var asm = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "VSSurvivalMod");

            if (asm == null)
            {
                DebugLogger.Error("BlockEntityGenerator | VSSurvivalMod assembly not loaded");
                throw new InvalidOperationException("VSSurvivalMod assembly is not loaded.");
            }

            Type? baseType = asm.GetType("Vintagestory.GameContent.BlockEntityGenericTypedContainer");

            if (baseType == null)
            {
                DebugLogger.Error("BlockEntityGenerator | Could not find typed container class in VSSurvivalMod");
                throw new InvalidOperationException(
                    "Could not find Vintagestory.GameContent.BlockEntityGenericTypedContainer in VSSurvivalMod."
                );
            }

            DebugLogger.Log($"BlockEntityGenerator | Resolved base type: {baseType.FullName}");
            DebugLogger.Log($"BlockEntityGenerator | Base type assembly: {baseType.Assembly.GetName().Name}");
            
            ConstructorInfo? baseCtor = baseType.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null
            );

            if (baseCtor == null)
            {
                DebugLogger.Error($"BlockEntityGenerator | No parameterless constructor found on {baseType.FullName}");
                throw new InvalidOperationException($"No parameterless constructor found on {baseType.FullName}");
            }

            var asmName = new AssemblyName("resourcecrates.Dynamic");
            AssemblyBuilder asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = asmBuilder.DefineDynamicModule(asmName.Name!);

            string generatedTypeName = "resourcecrates.Dynamic.BlockEntityResourceCrateDynamic";

            TypeBuilder tb = moduleBuilder.DefineType(
                generatedTypeName,
                TypeAttributes.Public | TypeAttributes.Class,
                baseType
            );

            // This call dynamically generates the entire class.
            AttachResourceCrateBridge(tb, baseType, baseCtor);
            
            generatedType = tb.CreateType();

            DebugLogger.Log($"BlockEntityGenerator | Generated type: {generatedType.FullName}");
            DebugLogger.Log($"BlockEntityGenerator | Generated type base: {generatedType.BaseType?.FullName}");
            DebugLogger.Log("BlockEntityGenerator | END dynamic BE generation");

            return generatedType;
        }
        
        
        private static void AttachResourceCrateBridge(
            TypeBuilder tb,
            Type baseType,
            ConstructorInfo baseCtor)
        {
            DebugLogger.Log("BlockEntityGenerator.AttachResourceCrateBridge START");

            // -----------------------------------------------------------------
            // Resolve compile-time-known types
            // -----------------------------------------------------------------

            Type hostInterfaceType = typeof(resourcecrates.BlockEntities.IResourceCrateHost);
            Type controllerType = typeof(resourcecrates.BlockEntities.BlockEntityResourceCrate);

            tb.AddInterfaceImplementation(hostInterfaceType);

            // -----------------------------------------------------------------
            // Private controller field:
            // private BlockEntityResourceCrate _controller;
            // -----------------------------------------------------------------

            FieldBuilder controllerField = tb.DefineField(
                "_controller",
                controllerType,
                FieldAttributes.Private
            );

            // -----------------------------------------------------------------
            // Resolve controller ctor: new BlockEntityResourceCrate(IResourceCrateHost)
            // -----------------------------------------------------------------

            ConstructorInfo controllerCtor = controllerType.GetConstructor(new[] { hostInterfaceType });
            if (controllerCtor == null)
            {
                throw new InvalidOperationException(
                    $"Could not find constructor {controllerType.FullName}({hostInterfaceType.FullName})"
                );
            }

            // -----------------------------------------------------------------
            // Resolve important inherited methods / properties from base chain
            // -----------------------------------------------------------------

            MethodInfo baseInitialize = FindRequiredMethod(
                baseType,
                "Initialize",
                typeof(void),
                typeof(ICoreAPI)
            );

            MethodInfo baseOnBlockPlaced = FindRequiredMethod(
                baseType,
                "OnBlockPlaced",
                typeof(void),
                typeof(ItemStack)
            );

            MethodInfo baseFromTreeAttributes = FindRequiredMethod(
                baseType,
                "FromTreeAttributes",
                typeof(void),
                typeof(ITreeAttribute),
                typeof(IWorldAccessor)
            );

            MethodInfo baseToTreeAttributes = FindRequiredMethod(
                baseType,
                "ToTreeAttributes",
                typeof(void),
                typeof(ITreeAttribute)
            );

            MethodInfo baseOnBlockUnloaded = FindRequiredMethod(
                baseType,
                "OnBlockUnloaded",
                typeof(void)
            );

            MethodInfo baseOnBlockRemoved = FindRequiredMethod(
                baseType,
                "OnBlockRemoved",
                typeof(void)
            );

            MethodInfo baseGetBlockInfo = FindRequiredMethod(
                baseType,
                "GetBlockInfo",
                typeof(void),
                typeof(IPlayer),
                typeof(System.Text.StringBuilder)
            );

            MethodInfo inheritedMarkDirty = FindRequiredMethod(
                baseType,
                "MarkDirty",
                typeof(void),
                typeof(bool),
                typeof(IPlayer)
            );

            MethodInfo inheritedRegisterTick = FindRequiredMethod(
                baseType,
                "RegisterGameTickListener",
                typeof(long),
                typeof(Action<float>),
                typeof(int),
                typeof(int)
            );

            MethodInfo inheritedUnregisterTick = FindRequiredMethod(
                baseType,
                "UnregisterGameTickListener",
                typeof(void),
                typeof(long)
            );

            MethodInfo inheritedGetApi = FindRequiredPropertyGetter(
                baseType,
                "Api",
                typeof(ICoreAPI)
            );

            MethodInfo inheritedGetInventory = FindPropertyGetterAnywhere(
                baseType,
                "Inventory"
            );

            if (inheritedGetInventory == null)
            {
                throw new InvalidOperationException(
                    $"Could not find Inventory property getter on {baseType.FullName} or its base types."
                );
            }

            MethodInfo itemSlotGetByIndex = inheritedGetInventory.ReturnType.GetMethod(
                "get_Item",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null
            );

            if (itemSlotGetByIndex == null)
            {
                throw new InvalidOperationException(
                    $"Could not find indexer getter on inventory type {inheritedGetInventory.ReturnType.FullName}."
                );
            }

            // -----------------------------------------------------------------
            // Constructor:
            // .ctor()
            // {
            //     base::.ctor();
            //     this._controller = new BlockEntityResourceCrate((IResourceCrateHost)this);
            // }
            // -----------------------------------------------------------------

            ConstructorBuilder ctor = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes
            );

            ILGenerator ctorIl = ctor.GetILGenerator();

            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, baseCtor);

            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Newobj, controllerCtor);
            ctorIl.Emit(OpCodes.Stfld, controllerField);

            ctorIl.Emit(OpCodes.Ret);

            // -----------------------------------------------------------------
            // Host interface methods
            // -----------------------------------------------------------------

            // ICoreAPI GetApi()
            DefineSimpleForwardMethod(
                tb,
                hostInterfaceType,
                "GetApi",
                typeof(ICoreAPI),
                Type.EmptyTypes,
                il =>
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, inheritedGetApi);
                    il.Emit(OpCodes.Ret);
                }
            );

            // ItemSlot GetOutputSlot()
            DefineSimpleForwardMethod(
                tb,
                hostInterfaceType,
                "GetOutputSlot",
                typeof(ItemSlot),
                Type.EmptyTypes,
                il =>
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, inheritedGetInventory);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Callvirt, itemSlotGetByIndex);
                    il.Emit(OpCodes.Ret);
                }
            );

            // void CallMarkDirty(bool redrawOnClient, IPlayer skipPlayer)
            DefineSimpleForwardMethod(
                tb,
                hostInterfaceType,
                "CallMarkDirty",
                typeof(void),
                new[] { typeof(bool), typeof(IPlayer) },
                il =>
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Call, inheritedMarkDirty);
                    il.Emit(OpCodes.Ret);
                }
            );

            // long CallRegisterGameTickListener(Action<float>, int)
            DefineSimpleForwardMethod(
                tb,
                hostInterfaceType,
                "CallRegisterGameTickListener",
                typeof(long),
                new[] { typeof(Action<float>), typeof(int), typeof(int) },
                il =>
                {
                    il.Emit(OpCodes.Ldarg_0); // this
                    il.Emit(OpCodes.Ldarg_1); // Action<float>
                    il.Emit(OpCodes.Ldarg_2); // interval
                    il.Emit(OpCodes.Ldarg_3); // initialDelay
                    il.Emit(OpCodes.Call, inheritedRegisterTick);
                    il.Emit(OpCodes.Ret);
                }
            );

            // void CallUnregisterGameTickListener(long)
            DefineSimpleForwardMethod(
                tb,
                hostInterfaceType,
                "CallUnregisterGameTickListener",
                typeof(void),
                new[] { typeof(long) },
                il =>
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, inheritedUnregisterTick);
                    il.Emit(OpCodes.Ret);
                }
            );

            // -----------------------------------------------------------------
            // Base wrapper host methods
            // These let the controller trigger base.* behavior legally.
            // -----------------------------------------------------------------

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseInitialize",
                baseInitialize,
                typeof(void),
                new[] { typeof(ICoreAPI) }
            );

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseOnBlockPlaced",
                baseOnBlockPlaced,
                typeof(void),
                new[] { typeof(ItemStack) }
            );

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseFromTreeAttributes",
                baseFromTreeAttributes,
                typeof(void),
                new[] { typeof(ITreeAttribute), typeof(IWorldAccessor) }
            );

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseToTreeAttributes",
                baseToTreeAttributes,
                typeof(void),
                new[] { typeof(ITreeAttribute) }
            );

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseOnBlockUnloaded",
                baseOnBlockUnloaded,
                typeof(void),
                Type.EmptyTypes
            );

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseOnBlockRemoved",
                baseOnBlockRemoved,
                typeof(void),
                Type.EmptyTypes
            );

            DefineBaseWrapperMethod(
                tb,
                hostInterfaceType,
                "CallBaseGetBlockInfo",
                baseGetBlockInfo,
                typeof(void),
                new[] { typeof(IPlayer), typeof(System.Text.StringBuilder) }
            );

            // -----------------------------------------------------------------
            // Controller-forwarded interface methods used by block code
            // -----------------------------------------------------------------

            DefineControllerForwardMethod(
                tb,
                hostInterfaceType,
                controllerField,
                "TryUpgrade",
                typeof(bool),
                new[] { typeof(IPlayer), typeof(ItemSlot) }
            );

            DefineControllerForwardMethod(
                tb,
                hostInterfaceType,
                controllerField,
                "TrySetOrReplaceTarget",
                typeof(bool),
                new[] { typeof(IPlayer), typeof(ItemSlot) }
            );

            DefineControllerForwardMethod(
                tb,
                hostInterfaceType,
                controllerField,
                "WriteCrateStateToItemStack",
                typeof(void),
                new[] { typeof(ItemStack) }
            );

            // -----------------------------------------------------------------
            // Virtual overrides that must exist on the generated BE itself
            // and forward into the controller.
            //
            // These are NOT base-wrapper methods. These are the real overrides
            // the engine will call.
            // -----------------------------------------------------------------

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseInitialize,
                "Initialize"
            );

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseOnBlockPlaced,
                "OnBlockPlaced"
            );

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseFromTreeAttributes,
                "FromTreeAttributes"
            );

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseToTreeAttributes,
                "ToTreeAttributes"
            );

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseOnBlockUnloaded,
                "OnBlockUnloaded"
            );

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseOnBlockRemoved,
                "OnBlockRemoved"
            );

            DefineControllerOverrideMethod(
                tb,
                controllerField,
                baseGetBlockInfo,
                "GetBlockInfo"
            );

            DebugLogger.Log("BlockEntityGenerator.AttachResourceCrateBridge END");
        }

        private static void DefineSimpleForwardMethod(
            TypeBuilder tb,
            Type interfaceType,
            string methodName,
            Type returnType,
            Type[] parameterTypes,
            Action<ILGenerator> emitBody)
        {
            MethodBuilder mb = tb.DefineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
                returnType,
                parameterTypes
            );

            ILGenerator il = mb.GetILGenerator();
            emitBody(il);

            MethodInfo ifaceMethod = interfaceType.GetMethod(methodName, parameterTypes);
            if (ifaceMethod == null)
            {
                throw new InvalidOperationException(
                    $"Could not find interface method {interfaceType.FullName}.{methodName}."
                );
            }

            tb.DefineMethodOverride(mb, ifaceMethod);
        }

        private static void DefineBaseWrapperMethod(
            TypeBuilder tb,
            Type interfaceType,
            string wrapperName,
            MethodInfo baseMethod,
            Type returnType,
            Type[] parameterTypes)
        {
            MethodBuilder mb = tb.DefineMethod(
                wrapperName,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
                returnType,
                parameterTypes
            );

            ILGenerator il = mb.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            for (short i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, (short)(i + 1));
            }

            // Critical: call the inherited/base implementation directly.
            il.Emit(OpCodes.Call, baseMethod);
            il.Emit(OpCodes.Ret);

            MethodInfo ifaceMethod = interfaceType.GetMethod(wrapperName, parameterTypes);
            if (ifaceMethod == null)
            {
                throw new InvalidOperationException(
                    $"Could not find interface method {interfaceType.FullName}.{wrapperName}."
                );
            }

            tb.DefineMethodOverride(mb, ifaceMethod);
        }

        private static void DefineControllerForwardMethod(
            TypeBuilder tb,
            Type interfaceType,
            FieldBuilder controllerField,
            string methodName,
            Type returnType,
            Type[] parameterTypes)
        {
            MethodInfo controllerMethod = FindRequiredMethod(
                controllerField.FieldType,
                methodName,
                returnType,
                parameterTypes
            );

            MethodBuilder mb = tb.DefineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
                returnType,
                parameterTypes
            );

            ILGenerator il = mb.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, controllerField);

            for (short i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, (short)(i + 1));
            }

            il.Emit(OpCodes.Callvirt, controllerMethod);
            il.Emit(OpCodes.Ret);

            MethodInfo ifaceMethod = interfaceType.GetMethod(methodName, parameterTypes);
            if (ifaceMethod == null)
            {
                throw new InvalidOperationException(
                    $"Could not find interface method {interfaceType.FullName}.{methodName}."
                );
            }

            tb.DefineMethodOverride(mb, ifaceMethod);
        }

        private static void DefineControllerOverrideMethod(
            TypeBuilder tb,
            FieldBuilder controllerField,
            MethodInfo baseVirtualMethod,
            string controllerMethodName)
        {
            Type[] parameterTypes = baseVirtualMethod.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();

            MethodInfo controllerMethod = FindRequiredMethod(
                controllerField.FieldType,
                controllerMethodName,
                baseVirtualMethod.ReturnType,
                parameterTypes
            );

            MethodBuilder mb = tb.DefineMethod(
                baseVirtualMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                baseVirtualMethod.ReturnType,
                parameterTypes
            );

            ILGenerator il = mb.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, controllerField);

            for (short i = 0; i < parameterTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, (short)(i + 1));
            }

            il.Emit(OpCodes.Callvirt, controllerMethod);
            il.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(mb, baseVirtualMethod);
        }

        private static MethodInfo FindRequiredMethod(
            Type startType,
            string name,
            Type returnType,
            params Type[] parameterTypes)
        {
            Type current = startType;

            while (current != null)
            {
                MethodInfo method = current.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    binder: null,
                    types: parameterTypes,
                    modifiers: null
                );

                if (method != null && method.ReturnType == returnType)
                {
                    return method;
                }

                current = current.BaseType;
            }

            string sig = $"{name}({string.Join(", ", parameterTypes.Select(t => t.Name))}) : {returnType.Name}";
            throw new InvalidOperationException($"Could not find required method {sig} starting from {startType.FullName}.");
        }

        private static MethodInfo FindRequiredPropertyGetter(
            Type startType,
            string propertyName,
            Type expectedPropertyType)
        {
            MethodInfo getter = FindPropertyGetterAnywhere(startType, propertyName);
            if (getter == null)
            {
                throw new InvalidOperationException(
                    $"Could not find property getter for '{propertyName}' starting from {startType.FullName}."
                );
            }

            if (getter.ReturnType != expectedPropertyType)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' had unexpected type {getter.ReturnType.FullName}; expected {expectedPropertyType.FullName}."
                );
            }

            return getter;
        }

        private static MethodInfo FindPropertyGetterAnywhere(Type startType, string propertyName)
        {
            Type current = startType;

            while (current != null)
            {
                PropertyInfo prop = current.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                );

                if (prop != null)
                {
                    MethodInfo getter = prop.GetGetMethod(nonPublic: true);
                    if (getter != null)
                    {
                        return getter;
                    }
                }

                current = current.BaseType;
            }

            return null;
        }
    }
}