using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using resourcecrates.Util;
using Vintagestory.API.Common;

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

            var asmName = new AssemblyName("resourcecrates.DynamicBlockEntities");
            AssemblyBuilder asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = asmBuilder.DefineDynamicModule(asmName.Name!);

            string generatedTypeName = "resourcecrates.Dynamic.BlockEntityResourceCrateDynamic";

            TypeBuilder tb = moduleBuilder.DefineType(
                generatedTypeName,
                TypeAttributes.Public | TypeAttributes.Class,
                baseType
            );

            ConstructorBuilder ctor = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes
            );

            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);

            generatedType = tb.CreateType();

            DebugLogger.Log($"BlockEntityGenerator | Generated type: {generatedType.FullName}");
            DebugLogger.Log($"BlockEntityGenerator | Generated type base: {generatedType.BaseType?.FullName}");
            DebugLogger.Log("BlockEntityGenerator | END dynamic BE generation");

            return generatedType;
        }
    }
}