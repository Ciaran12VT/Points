using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Points.Models
{
    public class ActiveCardModelWrapper
    {
        public string Type { get; set; }
        public JsonElement Data { get; set; }
    }
}
