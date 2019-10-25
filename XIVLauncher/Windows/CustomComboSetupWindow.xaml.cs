using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Dalamud.Discord;
using Dalamud.Game.Chat;
using XIVLauncher;
using XIVLauncher.Accounts;
using XIVLauncher.Addon;
using XIVLauncher.Dalamud;

namespace XIVLauncher.Windows
{
    public class CustomComboEntry
    {
        public CustomComboPreset Preset { get; set; } = CustomComboPreset.None;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int ClassJob { get; set; }

        public string Icon { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsCustomMessage { get; set; }
        public bool IsCheckboxEnabled => !IsCustomMessage;
    }

    public class ClassJobComboBoxEntry
    {
        public string Name { get; set; }
        public int ClassJob { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class CustomComboSetupWindow : Window
    {
        private List<CustomComboEntry> CustomCombos;

        public CustomComboPreset EnabledPresets = CustomComboPreset.None;

        public CustomComboSetupWindow(CustomComboPreset currentPresets)
        {
            InitializeComponent();

            ClassJobComboBox.ItemsSource = new List<ClassJobComboBoxEntry>
            {
                new ClassJobComboBoxEntry
                {
                    Name = "Filter for job...",
                    ClassJob = 0
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Dragoon",
                    ClassJob = 22
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Summoner",
                    ClassJob = 27
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Machinist",
                    ClassJob = 31
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Bard",
                    ClassJob = 23
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Black Mage",
                    ClassJob = 25
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Red Mage",
                    ClassJob = 35
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Dancer",
                    ClassJob = 38
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Samurai",
                    ClassJob = 34
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Ninja",
                    ClassJob = 30
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Monk",
                    ClassJob = 20
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Dark Knight",
                    ClassJob = 32
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Warrior",
                    ClassJob = 21
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Gunbreaker",
                    ClassJob = 37
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Astrologian",
                    ClassJob = 33
                },
                new ClassJobComboBoxEntry
                {
                    Name = "Scholar",
                    ClassJob = 28
                },
                new ClassJobComboBoxEntry
                {
                    Name = "White Mage",
                    ClassJob = 24
                }
            };

            EnabledPresets = currentPresets;

            CustomCombos = GetPresetList(currentPresets);

            CustomCombos.Add(new CustomComboEntry
            {
                ClassJob = 0,
                Description = "Join our discord to talk about them!",
                Name = "Any ideas for more custom combos or features?",
                Icon = "\uE901",
                IsCustomMessage = true
            });

            ComboListView.ItemsSource = CustomCombos;
            
            var view = CollectionViewSource.GetDefaultView(ComboListView.ItemsSource);
            view.Filter = ClassJobFilter;

            // We have to do this here, otherwise GetDefaultView will return null in InitializeComponent
            ClassJobComboBox.SelectionChanged += ClassJobComboBox_OnSelectionChanged;
        }

        private List<CustomComboEntry> GetPresetList(CustomComboPreset enabledPresets)
        {
            var comboList = new List<CustomComboEntry>();

            foreach (var presetValue in Enum.GetValues(typeof(CustomComboPreset)).Cast<CustomComboPreset>())
            {
                if (presetValue == CustomComboPreset.None)
                    continue;

                var presetInfo = presetValue.GetAttribute<CustomComboInfoAttribute>();
                comboList.Add(new CustomComboEntry
                {
                    Preset = presetValue,
                    Name = presetInfo.FancyName,
                    Description = presetInfo.Description,
                    ClassJob = presetInfo.ClassJob,
                    Icon = Util.ClassJobToIcon(presetInfo.ClassJob),
                    IsEnabled = enabledPresets.HasFlag(presetValue)
                });
            }

            return comboList;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var customComboEntry in CustomCombos.Where(customComboEntry => customComboEntry.IsEnabled && !customComboEntry.IsCustomMessage))
            {
                EnabledPresets |= customComboEntry.Preset;
            }

            Close();
        }

        private bool ClassJobFilter(object item)
        {
            var comboBoxItem = ClassJobComboBox.SelectedItem as ClassJobComboBoxEntry;
            var comboItem = item as CustomComboEntry;

            if (comboBoxItem.ClassJob == 0 || comboItem.IsCustomMessage)
                return true;
            else
                return comboItem.ClassJob == comboBoxItem.ClassJob;
        }

        private void ClassJobComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ComboListView.ItemsSource).Refresh();
        }

        private void EnableAll_OnClick(object sender, RoutedEventArgs e)
        {
            CustomCombos = CustomCombos.Select(c => {c.IsEnabled = true; return c;}).ToList();

            ComboListView.ItemsSource = CustomCombos;
        }

        private void DisableAll_OnClick(object sender, RoutedEventArgs e)
        {
            CustomCombos = CustomCombos.Select(c => {c.IsEnabled = false; return c;}).ToList();

            ComboListView.ItemsSource = CustomCombos;
        }
    }
}
