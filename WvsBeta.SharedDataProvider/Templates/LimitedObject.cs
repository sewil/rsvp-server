using System;
using System.Collections.Generic;
using System.Text;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Templates
{
    public  abstract class LimitedObject
    {
        public int? StartDate { get; set; }
        public int? EndDate { get; set; }
        
        public string LimitedName { get; set; }

        public void LoadLimitedDataFromProp(WzProperty prop)
        {
            if (prop == null) return;
            StartDate = prop.GetInt32("startDate");
            EndDate = prop.GetInt32("endDate");
            LimitedName = prop.GetString("limitedname");
        }

        public static Func<LimitedObject, bool> CheckIsActive = _ => false;
        public bool IsActive() => CheckIsActive(this);
    }
}
