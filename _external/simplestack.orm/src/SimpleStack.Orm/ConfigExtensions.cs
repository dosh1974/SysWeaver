using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using SimpleStack.Orm.Attributes;
using SimpleStack.Orm.Expressions.Statements;

namespace SimpleStack.Orm
{
    /// <summary>An ORM lite configuration extensions.</summary>
    internal static class OrmLiteConfigExtensions
    {
        /// <summary>The type model definition map.</summary>
        private static Dictionary<Tuple<Type, String>, ModelDefinition> _typeModelDefinitionMap = new Dictionary<Tuple<Type, string>, ModelDefinition>();

        /// <summary>Query if 'theType' is nullable type.</summary>
        /// <param name="theType">Type of the.</param>
        /// <returns>true if nullable type, false if not.</returns>
        private static bool IsNullableType(Type theType)
        {
            return theType.IsGenericType()
                   && theType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        /// <summary>A Type extension method that gets model definition.</summary>
        /// <param name="modelType">The modelType to act on.</param>
        /// <param name="tableName">Optionally override table name</param>
        /// <returns>The model definition.</returns>
        internal static ModelDefinition GetModelDefinition(this Type modelType, String tableName = null)
        {
            var key = Tuple.Create(modelType, tableName);
            if (_typeModelDefinitionMap.TryGetValue(key, out var modelDef))
            {
                return modelDef;
            }
            if (modelType.IsValueType() || modelType == typeof(string))
            {
                return null;
            }
            if (tableName != null)
            {
                var og = GetModelDefinition(modelType);
                modelDef = new ModelDefinition
                {
                    CompositeIndexes = og.CompositeIndexes,
                    FieldDefinitions = og.FieldDefinitions,
                    IgnoredFieldDefinitions = og.IgnoredFieldDefinitions,
                    PrimaryKeyType = og.PrimaryKeyType,
                    Schema = og.Schema,
                    ModelType = og.ModelType,
                    Name = tableName,
                    Alias = null,
                };
            }
            else
            {

                var modelAliasAttr = modelType.FirstAttribute<AliasAttribute>();
                var schemaAttr = modelType.FirstAttribute<SchemaAttribute>();
                modelDef = new ModelDefinition
                {
                    ModelType = modelType,
                    Name = String.IsNullOrEmpty(tableName) ? modelType.Name : tableName,
                    Alias = String.IsNullOrEmpty(tableName) ? modelAliasAttr?.Name : null,
                    PrimaryKeyType = modelType.FirstAttribute<PrimaryKeyTypeAttribute>()?.Type,
                    Schema = schemaAttr?.Name
                };

                modelDef.CompositeIndexes.AddRange(modelType.AlltAttributes<CompositeIndexAttribute>());

                var objProperties = modelType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance).ToList();

                Dictionary<String, FieldDefinition> flds = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
                foreach (var propertyInfo in objProperties)
                {
                    var sequenceAttr = propertyInfo.FirstAttribute<SequenceAttribute>();
                    var computeAttr = propertyInfo.FirstAttribute<ComputeAttribute>();
                    var pkAttribute = propertyInfo.FirstAttribute<PrimaryKeyAttribute>();
                    var decimalAttribute = propertyInfo.FirstAttribute<DecimalLengthAttribute>();

                    var isPrimaryKey = pkAttribute != null;

                    var isNullableType = IsNullableType(propertyInfo.PropertyType);

                    var isNullable = !propertyInfo.PropertyType.IsValueType()
                                     && propertyInfo.FirstAttribute<RequiredAttribute>() == null
                                     || isNullableType;

                    var propertyType = isNullableType
                        ? Nullable.GetUnderlyingType(propertyInfo.PropertyType)
                        : propertyInfo.PropertyType;

                    var aliasAttr = propertyInfo.FirstAttribute<AliasAttribute>();

                    var indexAttr = propertyInfo.FirstAttribute<IndexAttribute>();
                    var isIndex = indexAttr != null;
                    var isUnique = isIndex && indexAttr.Unique;

                    var stringLengthAttr = propertyInfo.FirstAttribute<StringLengthAttribute>();

                    var defaultValueAttr = propertyInfo.FirstAttribute<DefaultAttribute>();


                    var referencesAttr = propertyInfo.FirstAttribute<ReferencesAttribute>();
                    var foreignKeyAttr = propertyInfo.FirstAttribute<ForeignKeyAttribute>();

                    if (decimalAttribute != null && stringLengthAttr == null)
                    {
                        stringLengthAttr = new StringLengthAttribute(decimalAttribute.Precision);
                    }

                    var updateAggregateAttr = propertyInfo.FirstAttribute<UpdateAggregateAttribute>();
                    var fieldDefinition = new FieldDefinition
                    {
                        Name = propertyInfo.Name,
                        Alias = aliasAttr != null ? aliasAttr.Name : null,
                        FieldType = propertyType,
                        PropertyInfo = propertyInfo,
                        IsNullable = isNullable,
                        IsPrimaryKey = isPrimaryKey,
                        AutoIncrement = isPrimaryKey && propertyInfo.FirstAttribute<AutoIncrementAttribute>() != null,
                        IsIndexed = isIndex,
                        IsUnique = isUnique,
                        FieldLength = stringLengthAttr?.MaximumLength,
                        DefaultValue = defaultValueAttr?.DefaultValue,
                        UpdateAggregation = updateAggregateAttr?.Aggregate ?? UpdateAggregations.None,
                        UpdateAggregationData = updateAggregateAttr?.Data,
                        ForeignKey =
                            foreignKeyAttr == null
                                ? referencesAttr == null
                                    ? null
                                    : new ForeignKeyConstraint(referencesAttr.Type)
                                : new ForeignKeyConstraint(foreignKeyAttr.Type,
                                    foreignKeyAttr.OnDelete,
                                    foreignKeyAttr.OnUpdate,
                                    foreignKeyAttr.ForeignKeyName),
                        GetValueFn = propertyInfo.GetPropertyGetterFn(),
                        Sequence = sequenceAttr != null ? sequenceAttr.Name : string.Empty,
                        IsComputed = computeAttr != null,
                        ComputeExpression = computeAttr != null ? computeAttr.Expression : string.Empty,
                        Scale = decimalAttribute?.Scale,
                    };
                    flds.Add(fieldDefinition.Name, fieldDefinition);
                    if (propertyInfo.FirstAttribute<IgnoreAttribute>() != null)
                    {
                        modelDef.IgnoredFieldDefinitions.Add(fieldDefinition);
                    }
                    else
                    {
                        modelDef.FieldDefinitions.Add(fieldDefinition);
                    }
                }

                foreach (var x in flds)
                {
                    var at = x.Value.UpdateAggregation;
                    switch (at)
                    {
                        case UpdateAggregations.AddSameResetMax:
                        case UpdateAggregations.AddSameResetMin:
                        case UpdateAggregations.SetIfNewMax:
                        case UpdateAggregations.SetIfNewMin:
                        case UpdateAggregations.SetIfNewOrEqualMax:
                        case UpdateAggregations.SetIfNewOrEqualMin:
                            var t = x.Value.UpdateAggregationData?.First() as String;
                            if (t == null)
                                throw new Exception(nameof(UpdateAggregations) + "." + at + " must have a valid member name as the parameter (got null), found on " + modelType.FullName + "." + x.Key);
                            if (!flds.TryGetValue(t, out var y))
                                throw new Exception(nameof(UpdateAggregations) + "." + at + " must have a valid member name as the parameter (got \"" + t + "\"), found on " + modelType.FullName + "." + x.Key);
                            if (x.Value == y)
                                throw new Exception(nameof(UpdateAggregations) + "." + at + " may not have the same member as the parameter, found on " + modelType.FullName + "." + x.Key);
                            x.Value.UpdateAggregationData = new[] { y.FieldName };
                            var xi = modelDef.FieldDefinitions.IndexOf(x.Value);
                            var yi = modelDef.FieldDefinitions.IndexOf(y);
                            if (yi > xi)
                            {
                                modelDef.FieldDefinitions.RemoveAt(yi);
                                modelDef.FieldDefinitions.Insert(xi, y);
                            }
                        break;
                    }
                }

            }






            Dictionary<Tuple<Type, String>, ModelDefinition> snapshot, newCache;
            do
            {
                snapshot = _typeModelDefinitionMap;
                newCache = new Dictionary<Tuple<Type, String>, ModelDefinition>(_typeModelDefinitionMap);
                newCache[key] = modelDef;
            } while (!ReferenceEquals(
                Interlocked.CompareExchange(ref _typeModelDefinitionMap, newCache, snapshot), snapshot));

            return modelDef;
        }
    }
}