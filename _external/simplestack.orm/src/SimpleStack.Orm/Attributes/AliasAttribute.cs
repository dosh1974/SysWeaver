using System;

namespace SimpleStack.Orm.Attributes
{
    /// <summary>Attribute for alias.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class |
                    AttributeTargets.Struct)]
    public class AliasAttribute : Attribute
    {
	    /// <summary>Initializes a new instance of the NServiceKit.DataAnnotations.AliasAttribute class.</summary>
	    /// <param name="name">The name.</param>
	    public AliasAttribute(string name)
        {
            Name = name;
        }

	    /// <summary>Gets or sets the name.</summary>
	    /// <value>The name.</value>
	    public string Name { get; set; }
    }


    /// <summary>
    /// Specify to partition by key (if partition is performed)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class PartitionByKeyAttribute : Attribute
    {
        /// <summary>
        /// Partition preferences
        /// </summary>
        /// <param name="columnNames">Zero or more column names to use for partitioning (hash of their values is used), columns must be part of the primary key, for zero columns, the primary key is used</param>
        public PartitionByKeyAttribute(params String[] columnNames)
        {
            ColumnNames = columnNames ?? Array.Empty<String>();
        }
        public readonly string[] ColumnNames;
    }

    /// <summary>
    /// Specify to partition by hash (if partition is performed)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class PartitionByHashAttribute : Attribute
    {
        /// <summary>
        /// Partition preferences
        /// </summary>
        /// <param name="columnNames">One or more column name to use for partitioning</param>
        public PartitionByHashAttribute(params String[] columnNames)
        {
            ColumnNames = columnNames ?? Array.Empty<String>();
        }
        public readonly string[] ColumnNames;
    }



    public enum MySqlPrimaryKeyTypes
    {
        /// <summary>
        /// Default
        /// </summary>
        BTree = 0,
        /// <summary>
        /// Fast and smaller but only support equal and non-equal comaprision
        /// </summary>
        Hash = 1,
    }

    /// <summary>
    /// Specifies what type of index to use for the primary key
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class PrimaryKeyTypeAttribute: Attribute
    {
        /// <summary>
        /// The type of index to use for the primary key
        /// </summary>
        /// <param name="type">The type of index to use for the primary key</param>
        public PrimaryKeyTypeAttribute(String type)
        {
            Type = type;
        }

        /// <summary>
        /// The type of index to use for the primary key
        /// </summary>
        /// <param name="type">The type of index to use for the primary key</param>
        public PrimaryKeyTypeAttribute(MySqlPrimaryKeyTypes type)
        {
            Type = type.ToString().ToUpperInvariant();
        }

        public readonly String Type;
    }


}