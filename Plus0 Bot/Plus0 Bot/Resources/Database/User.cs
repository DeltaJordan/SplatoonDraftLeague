using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Plus0_Bot.Resources.Database
{
    public class User
    {
        [Key]
        public ulong UserId { get; set;  }
        public int Ammount { get; set; }
    }
}
