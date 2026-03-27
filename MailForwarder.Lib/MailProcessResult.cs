using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailForwarder.Lib
{
    public class MailProcessResult
    {
        public int MailsProcessed { get; set; }

        public bool IsSuccess { get; set; }
    }
}
