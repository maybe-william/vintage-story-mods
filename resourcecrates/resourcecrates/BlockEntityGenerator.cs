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

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in assemblies.OrderBy(a => a.GetName().Name))
            {
                DebugLogger.Log($"BlockEntityGenerator | Loaded assembly: {asm.GetName().Name}");
            }

            string[] exactTypeNames =
            {
                // Prefer the typed container first for chute compatibility
                "Vintagestory.GameContent.BlockEntityGenericTypedContainer",

                // Fallbacks for older or alternate names
                "Vintagestory.GameContent.BlockEntityGenericContainer",
                "Vintagestory.GameContent.BlockEntityContainerGeneric"
            };

            Type? baseType = null;

            // First pass: exact full-name lookup
            foreach (var asm in assemblies)
            {
                foreach (var typeName in exactTypeNames)
                {
                    baseType = asm.GetType(typeName, throwOnError: false);
                    if (baseType != null)
                    {
                        DebugLogger.Log($"BlockEntityGenerator | Exact match found: {baseType.FullName} in {asm.GetName().Name}");
                        break;
                    }
                }

                if (baseType != null) break;
            }

            // Second pass: fallback by simple type name
            if (baseType == null)
            {
                foreach (var asm in assemblies)
                {
                    Type[] types;

                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                    }

                    foreach (var t in types)
                    {
                        if (t == null) continue;

                        if (t.IsClass &&
                            !t.IsSealed &&
                            (
                                t.Name == "BlockEntityGenericTypedContainer" ||
                                t.Name == "BlockEntityGenericContainer" ||
                                t.Name == "BlockEntityContainerGeneric"
                            ))
                        {
                            DebugLogger.Log($"BlockEntityGenerator | Fallback candidate found: {t.FullName} in {t.Assembly.GetName().Name}");
                            baseType = t;
                            break;
                        }
                    }

                    if (baseType != null) break;
                }
            }

            if (baseType == null)
            {
                DebugLogger.Error("BlockEntityGenerator | Could not find runtime generic typed/generic container block entity type in any loaded assembly");
                throw new InvalidOperationException("Could not find runtime generic typed/generic container block entity type.");
            }

            DebugLogger.Log($"BlockEntityGenerator | Resolved base type: {baseType.FullName}");
            DebugLogger.Log($"BlockEntityGenerator | Base type assembly: {baseType.Assembly.GetName().Name}");

            if (baseType.IsSealed)
            {
                DebugLogger.Error($"BlockEntityGenerator | Base type is sealed: {baseType.FullName}");
                throw new InvalidOperationException($"Base type is sealed: {baseType.FullName}");
            }

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