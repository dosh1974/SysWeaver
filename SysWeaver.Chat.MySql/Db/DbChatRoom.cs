using SimpleStack.Orm.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Chat.MySql
{

    [Alias("Rooms")]
    [PartitionByKey(nameof(Id))]
    class DbChatRoom
    {
        /// <summary>
        /// Unqiue chat id
        /// </summary>
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }

        /// <summary>
        /// Name of the room
        /// </summary>
        [Ascii]
        [Required]
        [Index]
        [StringLength(64)]
        public String Name { get; set; }

        /// <summary>
        /// When the room was created
        /// </summary>
        [Required]
        [Index]
        [Default("1974-11-18")]
        public DateTime Created { get; set; }

        /// <summary>
        /// When the room was used
        /// </summary>
        [Required]
        [Index]
        [Default("1974-11-18")]
        public DateTime Used { get; set; }


    }


}
