using System;
using log4net;
using WvsBeta.Common;
using System.Collections.Generic;

namespace WvsBeta.Game
{
    class CheatInspector
    {

        public static bool CheckSpeed(Pos PixelsPerSecond, float pAllowedSpeed)
        {
            float speedMod = Math.Abs(PixelsPerSecond.X) / 125f;
            return speedMod < pAllowedSpeed + 0.1f;
        }
        
        public static bool CheckTextSpam(string text) //Unlimited text hacks
        {
            // Admin clients have a limit of 256
            return text.Length > 140;
        }


    }
    
    public class AbuseReport
    {
        public string reported_charname { get; set; }
        public int reported_charid { get; set; }
        public int reported_userid { get; set; }
        public string reporter_charname { get; set; }
        public int reporter_charid { get; set; }
        public int reporter_userid { get; set; }
        public int mapid { get; set; }
        public int reason { get; set; }
        public string reasontext { get; set; }
        public string report_date { get; set; }

        public AbuseReport(
            string reporter_charname, int reporter_charid, int reporter_userid,
            string reported_charname, int reported_charid, int reported_userid,
            int mapid, int reason, string reasontext, DateTime date)
        {
            this.reported_charname = reported_charname;
            this.reported_charid = reported_charid;
            this.reported_userid = reported_userid;
            this.reporter_charname = reporter_charname;
            this.reporter_charid = reporter_charid;
            this.reporter_userid = reporter_userid;
            this.mapid = mapid;
            this.reason = reason;
            this.reasontext = reasontext;
            this.report_date = date.ToString();
        }

        public override string ToString()
        {
            return $"[{report_date}] Report by {reporter_charname} (ID: {reporter_charid}), map {mapid} about {reported_charname} (ID: {reported_charid}), reason {reasontext}";
        }
    }
}
