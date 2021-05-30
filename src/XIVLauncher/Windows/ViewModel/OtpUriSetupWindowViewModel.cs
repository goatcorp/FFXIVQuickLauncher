using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class OtpUriSetupWindowViewModel
    {
        public OtpUriSetupWindowViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            OtpUriSetupTitleLoc = Loc.Localize("OtpUriSetupTitle", "Configure OTP URI");
            OtpUriSetupDescriptionLoc = Loc.Localize("OtpUriSetupDescription",
                "Please enter the complete URI from the 2D Barcode provided when setting up your 2FA.\r\nThis value should be stored otherwise in case this value is lost.");
            SecretDescLoc = Loc.Localize("SecretDescLoc", "2FA Secret");
            PeriodDescLoc = Loc.Localize("PeriodDescLoc", "Refresh Period");
            LengthDescloc = Loc.Localize("LengthDescloc", "Code Length");
            AlgorithmDescLoc = Loc.Localize("AlgorithmDescLoc", "OTP Method");
            OtpCodeDescLoc = Loc.Localize("OtpCodeDescLoc", "Current Auth Code");
            UnknownValueLoc = Loc.Localize("UnknowValueLoc", "<Unknown>");
            OkLoc = Loc.Localize("OK", "OK");
        }

        public string OtpUriSetupTitleLoc { get; private set; }
        public string OtpUriSetupDescriptionLoc { get; private set; }
        public string SecretDescLoc { get; private set; }
        public string PeriodDescLoc { get; private set; }
        public string LengthDescloc { get; private set; }
        public string AlgorithmDescLoc { get; private set; }
        public string OtpCodeDescLoc { get; private set; }
        public string UnknownValueLoc { get; private set; }
        public string OkLoc { get; private set; }
    }
}
