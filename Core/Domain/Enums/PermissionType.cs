using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum Permission
    {

        DEVICE_VIEW,
        DEVICE_CONTROL,
        DEVICE_SERVICE,

        USER_VIEW,
        USER_BLOCK,

        BALANCE_TOPUP,
        BALANCE_VIEW,

        REPORT_VIEW
    }
}
