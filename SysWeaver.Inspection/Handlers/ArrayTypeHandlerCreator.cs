using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Inspection.Implementation
{
    static class ArrayTypeHandlerCreator<T>
    {

        private static readonly MethodInfo GetLength = typeof(Array).GetTypeInfo().GetDeclaredMethod("GetLength");

        public static TypeHandler<T>.CreateDelegate MakeCreator(Type type, ParameterExpression inspector, ParameterExpression version, ParameterExpression isLatestVersion)
        {
            bool isBA = type == typeof(Byte[]);
            var array = Expression.Variable(type, "array");
            var l = Expression.Lambda<TypeHandler<T>.CreateDelegate>(
                Expression.Block(new ParameterExpression[] { array },
                isBA ? MakeByteArrayBody(type, inspector, array, true) : MakeBody(type, inspector, array, true),
                    array
                    ), inspector, version, isLatestVersion);
            return l.Compile();
        }


        public static TypeHandler<T>.DescribeDelegate MakeDescriptor(Type type, ParameterExpression inspector, ParameterExpression array, ParameterExpression version)
        {
            bool isBA = type == typeof(Byte[]);
            var l = Expression.Lambda<TypeHandler<T>.DescribeDelegate>(
                isBA ? MakeByteArrayBody(type, inspector, array, false) : MakeBody(type, inspector, array, false), 
                inspector, array, version);
            return l.Compile();
        }

        public static TypeHandler<T>.DescribeDelegate MakeWrappedDescriptor(Type callType, Type type, ParameterExpression inspector, ParameterExpression array, ParameterExpression version)
        {
            bool isBA = type == typeof(Byte[]) && (array.Type == type);
            var tempA = Expression.Variable(callType);
            var l = Expression.Lambda<TypeHandler<T>.DescribeDelegate>(
                Expression.Block(new ParameterExpression[] { tempA },
                    Expression.Assign(tempA, array),
                    isBA ? MakeByteArrayBody(type, inspector, tempA, false) : MakeBody(type, inspector, tempA, false),
                    Expression.Assign(array, Expression.Convert(tempA, callType))
                    ), inspector, array, version);
            return l.Compile();
        }

        /// <summary>
        /// The body for a byte array: the length (<see cref="IInspectorImplementation.Array_Length(ref int)"/>, a local, nothing is allocated), a new array if the length differs (or the array is null), then the bytes as one block
        /// </summary>
        private static Expression MakeByteArrayBody(Type type, Expression inspector, Expression array, bool forceNull)
        {
            var isDiff = Expression.Variable(typeof(bool), "isDiff");
            var notNull = Expression.Variable(typeof(bool), "notNull");
            var length = Expression.Variable(typeof(int), "length");
            var arrayLength = InspectorImplementationInfo<IInspectorImplementation>.ArrayLength;
            var len = Expression.Property(array, "Length");
            var body = Expression.Block(new[] { length, isDiff, notNull },
                Expression.Assign(isDiff, Expression.Constant(true)),
                Expression.Assign(notNull, Expression.NotEqual(array, Expression.Constant(null))),
                Expression.IfThenElse(notNull,
                    Expression.Block(
                        Expression.Assign(length, len),
                        Expression.Call(inspector, arrayLength, length),
                        Expression.Assign(isDiff, Expression.NotEqual(len, length))
                    ),
                    Expression.Block(
                        Expression.Assign(length, Expression.Constant(-1)),
                        Expression.Call(inspector, arrayLength, length)
                    )
                ),
                Expression.IfThen(isDiff, Expression.Block(
                    Expression.Assign(array, Expression.NewArrayBounds(typeof(Byte), length)),
                    Expression.Call(inspector, InspectorInfo<IInspector>.OnNew.MakeGenericMethod(type), array, notNull)
                )),
                Expression.Call(inspector, InspectorImplementationInfo<IInspectorImplementation>.ArrayByteArray, length, array)
            );
            return body;
        }

        /// <summary>
        /// The body for a one dimensional array: the same as <see cref="MakeBody"/>, but the length is a local (<see cref="IInspectorImplementation.Array_Length(ref int)"/>),
        /// so describing an array doesn't allocate the two length arrays
        /// </summary>
        static Expression MakeBody1(Type type, Expression inspector, Expression array, bool forceNull, Func<Expression, Expression> writeArray)
        {
            var elementType = type.GetElementType();
            var arrayLength = InspectorImplementationInfo<IInspectorImplementation>.ArrayLength;
            var length = Expression.Variable(typeof(int), "length");
            var prev = Expression.Variable(typeof(int), "prev");
            var i = Expression.Variable(typeof(int), "i0");
            var isDiff = Expression.Variable(typeof(bool), "isDiff");
            var notNull = Expression.Variable(typeof(bool), "notNull");
            var brk = Expression.Label("break0");
            var fieldMethod = InspectorInfo<IInspector>.GetRegFieldMethod(elementType);
            var loop = Expression.Block(
                Expression.Assign(i, Expression.Constant(0)),
                Expression.Loop(
                    Expression.IfThenElse(Expression.LessThan(i, length),
                        Expression.Block(Expression.Call(inspector, fieldMethod, Expression.ArrayAccess(array, i)), Expression.AddAssign(i, Expression.Constant(1))),
                        Expression.Break(brk)),
                    brk));
            if (forceNull)
            {
                return Expression.Block(new[] { length, i },
                    Expression.Assign(length, Expression.Constant(-1)),
                    Expression.Call(inspector, arrayLength, length),
                    Expression.Assign(array, Expression.NewArrayBounds(elementType, length)),
                    Expression.Call(inspector, InspectorInfo<IInspector>.OnNew.MakeGenericMethod(type), array, Expression.Constant(false)),
                    loop);
            }
            return Expression.Block(new[] { length, prev, i, isDiff, notNull },
                Expression.Assign(isDiff, Expression.Constant(true)),
                Expression.Assign(notNull, Expression.NotEqual(array, Expression.Constant(null))),
                Expression.IfThenElse(notNull,
                    Expression.Block(
                        Expression.Assign(prev, Expression.Call(array, GetLength, Expression.Constant(0))),
                        Expression.Assign(length, prev),
                        Expression.Call(inspector, arrayLength, length),
                        Expression.Assign(isDiff, Expression.NotEqual(prev, length))
                    ),
                    Expression.Block(
                        Expression.Assign(length, Expression.Constant(-1)),
                        Expression.Call(inspector, arrayLength, length)
                    )
                ),
                Expression.IfThen(isDiff, Expression.Block(
                    writeArray(Expression.NewArrayBounds(elementType, length)),
                    Expression.Call(inspector, InspectorInfo<IInspector>.OnNew.MakeGenericMethod(type), array, notNull)
                )),
                loop);
        }

        static Expression MakeBody(Type type, Expression inspector, Expression array, bool forceNull)
        {
            Func<Expression, Expression> writeArray = x => Expression.Assign(array, x);
            if (array.Type != type)
            {
                writeArray = x => Expression.Convert(x, array.Type);
                array = Expression.Convert(array, type);
            }
            int rank = type.GetArrayRank();
            //  One dimensional arrays (also a T[*] of rank 1, created with NewArrayBounds like before) use locals for the length
            if (rank == 1)
                return MakeBody1(type, inspector, array, forceNull, writeArray);
            var elementType = type.GetElementType();

            var isDiff = Expression.Variable(typeof(bool), "isDiff");
            var notNull = Expression.Variable(typeof(bool), "notNull");
            var negOne = Expression.Constant(-1);

            var lengths = Expression.Variable(typeof(int[]), "lengths");
            var getLengthFromNull = new List<Expression>();
            var arrayInit = new Expression[rank];
            for (int i = 0; i < rank; ++ i)
                arrayInit[i] = negOne;
            getLengthFromNull.Add(Expression.Assign(lengths, Expression.NewArrayInit(typeof(int), arrayInit)));
            getLengthFromNull.Add(Expression.Call(inspector, InspectorImplementationInfo<IInspectorImplementation>.ArrayLenghts, lengths));
            var prevL = Expression.Variable(typeof(int[]), "prevL");
            var getLength = new List<Expression>();
            var arrayCopy = new Expression[rank];
            for (int i = 0; i < rank; ++i)
            {
                arrayInit[i] = Expression.Call(array, GetLength, Expression.Constant(i));
                arrayCopy[i] = Expression.ArrayAccess(lengths, Expression.Constant(i));
            }
            getLength.Add(Expression.Assign(lengths, Expression.NewArrayInit(typeof(int), arrayInit)));
            getLength.Add(Expression.Assign(prevL, Expression.NewArrayInit(typeof(int), arrayCopy)));
            getLength.Add(Expression.Call(inspector, InspectorImplementationInfo<IInspectorImplementation>.ArrayLenghts, lengths));
            for (int d = 0; d < rank; ++d)
            {
                if (d == 0)
                    getLength.Add(Expression.Assign(isDiff, Expression.NotEqual(Expression.ArrayAccess(prevL, Expression.Constant(d)), Expression.ArrayAccess(lengths, Expression.Constant(d)))));
                else
                    getLength.Add(Expression.OrAssign(isDiff, Expression.NotEqual(Expression.ArrayAccess(prevL, Expression.Constant(d)), Expression.ArrayAccess(lengths, Expression.Constant(d)))));
            }
            var counters = new ParameterExpression[rank];
            var labels = new LabelTarget[rank];
            for (int d = 0; d < rank; ++ d)
            {
                counters[d] = Expression.Variable(typeof(int), "i" + d);
                labels[d] = Expression.Label("break" + d);
            }
            MethodInfo fieldMethod = InspectorInfo<IInspector>.GetRegFieldMethod(elementType);
            Expression desc = Expression.Call(inspector, fieldMethod, Expression.ArrayAccess(array, counters));
            int dd = rank;
            while (dd > 0)
            {
                -- dd;
                if (dd > 0)
                {
                    desc = Expression.Block(
                    Expression.Call(inspector, InspectorImplementationInfo<IInspectorImplementation>.ArrayLevelUp, Expression.Constant(dd)),
                    Expression.Assign(counters[dd], Expression.Constant(0)),
                    Expression.Loop(
                        Expression.IfThenElse(Expression.LessThan(counters[dd], Expression.ArrayAccess(lengths, Expression.Constant(dd))),
                        Expression.Block(desc, Expression.AddAssign(counters[dd], Expression.Constant(1))),
                        Expression.Break(labels[dd])),
                        labels[dd]),
                    Expression.Call(inspector, InspectorImplementationInfo<IInspectorImplementation>.ArrayLevelDown, Expression.Constant(dd))
                        );
                }else {
                    desc = Expression.Loop(
                        Expression.IfThenElse(Expression.LessThan(counters[dd], Expression.ArrayAccess(lengths, Expression.Constant(dd))),
                        Expression.Block(desc, Expression.AddAssign(counters[dd], Expression.Constant(1))),
                        Expression.Break(labels[dd])),
                        labels[dd]);
                }
            }
            var allParams = new List<ParameterExpression>(counters);
            allParams.Add(lengths);
            allParams.Add(prevL);
            if (forceNull)
            {
                var bodyNull = Expression.Block(allParams,
                    Expression.Block(getLengthFromNull),
                    Expression.Assign(array, Expression.NewArrayBounds(elementType, arrayCopy)),
                    Expression.Call(inspector, InspectorInfo<IInspector>.OnNew.MakeGenericMethod(type), array, Expression.Constant(false)),
                    Expression.Assign(counters[0], Expression.Constant(0)),
                    desc
                    );                       
                return bodyNull;
            }
            allParams.Add(isDiff);
            allParams.Add(notNull);
               
            var body = Expression.Block(allParams,
                Expression.Assign(isDiff, Expression.Constant(true)),
                Expression.Assign(notNull, Expression.NotEqual(array, Expression.Constant(null))),
                Expression.IfThenElse(notNull, 
                    Expression.Block(getLength)
                    ,
                    Expression.Block(getLengthFromNull)
                ),
                        Expression.IfThen(isDiff, Expression.Block(
                        writeArray(Expression.NewArrayBounds(elementType, arrayCopy)),
                        Expression.Call(inspector, InspectorInfo<IInspector>.OnNew.MakeGenericMethod(type), array, notNull)
                    )),
                    Expression.Assign(counters[0], Expression.Constant(0)),
                    desc
//                    ))
                );                       
            return body;
        }
    }

}