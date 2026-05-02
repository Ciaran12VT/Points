using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models.DbModels
{
    public class CardDbModel
    {
        public int CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string Title { get; set; } = "";
        public string Tags { get; set; } = "";
    }

}
