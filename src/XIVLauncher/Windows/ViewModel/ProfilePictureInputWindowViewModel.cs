using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class ProfilePictureInputWindowViewModel
    {
        public ProfilePictureInputWindowViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            ProfilePictureInputTitleLoc = Loc.Localize("ProfilePictureInputTitle", "Configure profile picture");
            ProfilePictureInputDescriptionLoc = Loc.Localize("ProfilePictureInputDescription", "Please enter a character's name and world.");
            CharacterNameLoc = Loc.Localize("CharacterName", "Character Name");
            WorldNameLoc = Loc.Localize("WorldName", "World Name");
            OkLoc = Loc.Localize("OK", "OK");
        }

        public string ProfilePictureInputTitleLoc { get; private set; }
        public string ProfilePictureInputDescriptionLoc { get; private set; }
        public string CharacterNameLoc { get; private set; }
        public string WorldNameLoc { get; private set; }
        public string OkLoc { get; private set; }
    }
}
