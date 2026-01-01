using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public interface ICardModel
    {
        int Id { get; set; }
        string Title { get; }
        string Tags { get; }
        double GetValue(DateTime start, DateTime end);
    }
}
