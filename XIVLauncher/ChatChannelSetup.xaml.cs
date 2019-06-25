using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Dalamud.Discord;
using Dalamud.Game.Chat;
using XIVLauncher;
using XIVLauncher.Addon;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class ChatChannelSetup : Window
    {
        public ChatTypeConfiguration Result { get; private set; }

        private int _color;

        internal class ChatTypeComboBoxWrapper
        {
            public string Name { get; set; }
            public XivChatType ChatType { get; set; }

            public override string ToString() => Name;
        }

        public ChatChannelSetup(ChatTypeConfiguration chatTypeConfig = null)
        {
            InitializeComponent();

            foreach (var xivChatType in Enum.GetValues(typeof(XivChatType)).Cast<XivChatType>())
            {
                var details = xivChatType.GetDetails();

                if (details == null)
                    continue;

                ChatTypeComboBox.Items.Add(new ChatTypeComboBoxWrapper
                {
                    Name = details.FancyName,
                    ChatType = xivChatType
                });
            }

            if (chatTypeConfig != null)
            {
                ChatTypeComboBox.SelectedIndex = ChatTypeComboBox.Items.Cast<ChatTypeComboBoxWrapper>()
                    .Select((v,i) => new {Index = i, Value = v}) // Pair up values and indexes
                    .Where(p => p.Value.ChatType == chatTypeConfig.ChatType) // Do the filtering
                    .Select(p => p.Index)
                    .First();

                ChannelTypeComboBox.SelectedIndex = (int) chatTypeConfig.Channel.Type;
                ChannelIdTextBox.Text = chatTypeConfig.Channel.ChannelId.ToString();

                ApplyColor(chatTypeConfig.Color);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(ChannelIdTextBox.Text))
                Close();

            var comboBoxEntry = ChatTypeComboBox.SelectedItem as ChatTypeComboBoxWrapper;

            if (!string.IsNullOrEmpty(ChannelIdTextBox.Text) && ulong.TryParse(ChannelIdTextBox.Text, out var channelId))
            {
                Result = new ChatTypeConfiguration
                {
                    Channel = new ChannelConfiguration
                    {
                        ChannelId = channelId,
                        GuildId = 0,
                        Type = (ChannelType) ChannelTypeComboBox.SelectedIndex
                    },
                    Color = _color,
                    ChatType = comboBoxEntry.ChatType
                };

                Close();
                return;
            }

            MessageBox.Show("Please enter valid IDs.", "XIVLauncher problem", MessageBoxButton.OK,
                MessageBoxImage.Error);
            this.Close();
        }

        private void OpenGuideLabel_OnClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher/wiki/How-to-get-discord-ids");
        }

        private void ChatTypeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBoxEntry = ChatTypeComboBox.SelectedItem as ChatTypeComboBoxWrapper;

            var value = (int) comboBoxEntry.ChatType.GetDetails().DefaultColor;
            ApplyColor(value);
        }

        private void ColorPicker_OnSelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            ApplyColor(Util.ColorToArgb(e.NewValue.Value));
        }

        private void ApplyColor(int argb)
        {
            var color = Util.ColorFromArgb(argb);
            var brush = new SolidColorBrush(color);

            ChannelColorIcon.Foreground = brush;
            ChannelColorIcon.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => {}));

            ColorPicker.SelectedColor = color;

            _color = argb;
        }
    }
}
