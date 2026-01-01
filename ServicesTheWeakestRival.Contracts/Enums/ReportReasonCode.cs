using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesTheWeakestRival.Contracts.Enums
{
    public enum ReportReasonCode : byte
    {
        Harassment = 1,
        Cheating = 2,
        Spam = 3,
        InappropriateName = 4,
        Other = 5
    }
}
