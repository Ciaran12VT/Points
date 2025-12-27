using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Interfaces
{
    public interface IAudioFeedback
    {
        void Tick();   // quiet click when moving between items/pages
        void Clack();
        void Thock();  // tap on card
    }

}
