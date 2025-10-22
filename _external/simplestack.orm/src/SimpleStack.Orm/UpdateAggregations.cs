namespace SimpleStack.Orm
{
    public enum UpdateAggregations
    {
        None = 0,
        /// <summary>
        /// Data: none.
        /// Upsert: Row.Column += Insert.Column.
        /// Select: Sum(Column).
        /// </summary>
        Add,
        /// <summary>
        /// Data: none.
        /// Upsert: Row.Column -= Insert.Column.
        /// Select: n/a.
        /// </summary>
        Sub,
        /// <summary>
        /// Data: none.
        /// Upsert: Row.Column = Min(Row.Column, Insert.Column).
        /// Select: Min(Column).
        /// </summary>
        Min,
        /// <summary>
        /// Data: none.
        /// Upsert: Row.Column = Max(Row.Column, Insert.Column).
        /// Select: Max(Column).
        /// </summary>
        Max,
        /// <summary>
        /// Data: none.
        /// Upsert: Row.Column = Insert.Column
        /// Select: n/a
        /// </summary>
        Set,

        /// <summary>
        /// Data: TargetColumn name
        /// Upsert: Row.Column = Insert.TargetColumn > Row.TargetColumn ? Insert.Column : (Insert.TargetColumn == Row.TargetColumn ? (Row.Column + Insert.Column) : Row.Column).
        /// Select: Using sub query
        /// </summary>
        AddSameResetMax,

        /// <summary>
        /// Data: TargetColumn name
        /// Upsert: Row.Column = Insert.TargetColumn < Row.TargetColumn ? Insert.Column : (Insert.TargetColumn == Row.TargetColumn ? (Row.Column + Insert.Column) : Row.Column).
        /// Select: Using sub query
        /// </summary>
        AddSameResetMin,

        /// <summary>
        /// Data: TargetColumn name
        /// Upsert: Row.Column = Insert.TargetColumn > Row.TargetColumn ? Insert.Column : Row.Column.
        /// Select: Using sub query
        /// </summary>
        SetIfNewMax,

        /// <summary>
        /// Data: TargetColumn name
        /// Upsert: Row.Column = Insert.TargetColumn < Row.TargetColumn ? Insert.Column : Row.Column.
        /// Select: Using sub query
        /// </summary>
        SetIfNewMin,

        /// <summary>
        /// Data: TargetColumn name
        /// Upsert: Row.Column = Insert.TargetColumn >= Row.TargetColumn ? Insert.Column : Row.Column.
        /// Select: Using sub query
        /// </summary>
        SetIfNewOrEqualMax,

        /// <summary>
        /// Data: TargetColumn name
        /// Upsert: Row.Column = Insert.TargetColumn <= Row.TargetColumn ? Insert.Column : Row.Column.
        /// Select: Using sub query
        /// </summary>
        SetIfNewOrEqualMin,
    }
}