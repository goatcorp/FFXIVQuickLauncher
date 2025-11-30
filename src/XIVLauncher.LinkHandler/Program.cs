using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Serilog;
using Serilog.Events;
using XIVLauncher.Common.Dalamud.Rpc;
using XIVLauncher.Common.Util;

namespace XIVLauncher.LinkHandler;

public static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Fatal)
            .WriteTo.Debug()
            .MinimumLevel.Verbose()
            .CreateLogger();

        var linkArgument = new Argument<string>(
            name: "link",
            description: "The Dalamud link to handle");

        var searchPathOption = new Option<string?>(
            aliases: new[] { "--search-path", "-d" },
            description: "Custom directory to search for Dalamud RPC sockets",
            getDefaultValue: () => null);

        var rootCommand = new RootCommand("XIVLauncher Link Handler - Sends Dalamud links to running game instances")
        {
            linkArgument,
            searchPathOption
        };

        rootCommand.SetHandler(async (link, searchPath) => { await HandleLinkAsync(link, searchPath); }, linkArgument, searchPathOption);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> HandleLinkAsync(string link, string? searchPath)
    {
        Log.Information("Handling link: {Link}", link);

        if (!string.IsNullOrEmpty(searchPath))
        {
            Log.Information("Using custom search path: {SearchPath}", searchPath);
        }

        var discoveryService = new DalamudRpcDiscovery(searchPath);
        var discoveredClients = new List<DiscoveredClient>();

        try
        {
            await foreach (var client in discoveryService.SearchAsync())
            {
                Log.Information("Discovered client: {ClientId}", client.HelloResponse.ProcessId);
                discoveredClients.Add(client);
            }

            if (discoveredClients.Count == 0)
            {
                Log.Warning("No Dalamud clients found");
                ShowErrorDialog("No Running Game Instances",
                    "No running instances of FINAL FANTASY XIV with a Dalamud capable of handling links were found.",
                    TaskDialogIcon.Warning);
                return 1;
            }

            var selectedClient = await ShowClientSelectionDialog(link, discoveredClients);

            if (selectedClient != null)
            {
                try
                {
                    Log.Information("Sending link to client: {ClientId}", selectedClient.HelloResponse.ProcessId);
                    await selectedClient.Client.Proxy.HandleLinkAsync(link);
                    Log.Information("Link sent successfully");
                    return 0;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to send link to client");
                    ShowErrorDialog("Failed to Send Link", $"An error occurred while sending the link:\n{ex.Message}");
                    return 1;
                }
            }
            else
            {
                Log.Information("User cancelled the operation");
                return 0;
            }
        }
        finally
        {
            // Clean up all discovered clients
            foreach (var client in discoveredClients)
            {
                await client.DisposeAsync();
            }
        }
    }

    private static Task<DiscoveredClient?> ShowClientSelectionDialog(string link, List<DiscoveredClient> clients)
    {
        var tcs = new TaskCompletionSource<DiscoveredClient?>();

        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var sendLinkButton = new TaskDialogCommandLinkButton { Text = "Open Link" };
                var closeDialogButton = new TaskDialogCommandLinkButton { Text = "Don't Open Link", AllowCloseDialog = true };

                var taskDialog = new TaskDialogPage
                {
                    Caption = "XIVLauncher Link Handler",
                    Heading = "Send Link to Dalamud?",
                    Icon = TaskDialogIcon.Information,
                    Text = $"An external application wants to send the following link to Dalamud:\n\n{link}",
                    AllowCancel = true,
                    Buttons =
                    {
                        sendLinkButton,
                        closeDialogButton
                    },
                    DefaultButton = sendLinkButton
                };

                if (clients.Count > 1)
                {
                    taskDialog.Text += "\n\nMultiple game instances are running. Please select a client to open this link:";

                    foreach (var client in clients)
                    {
                        var displayText = GetClientDisplayText(client);
                        var radioButton = new TaskDialogRadioButton(displayText);
                        taskDialog.RadioButtons.Add(radioButton);
                    }

                    // Select the first one by default
                    taskDialog.RadioButtons[0].Checked = true;
                }

                var result = TaskDialog.ShowDialog(taskDialog);

                if (result == sendLinkButton)
                {
                    if (clients.Count == 1)
                    {
                        tcs.SetResult(clients[0]);
                    }
                    else
                    {
                        // Find which radio button was selected
                        var checkedButton = taskDialog.RadioButtons.FirstOrDefault(rb => rb.Checked);

                        if (checkedButton != null)
                        {
                            var index = taskDialog.RadioButtons.IndexOf(checkedButton);
                            tcs.SetResult(clients[index]);
                        }
                        else
                        {
                            tcs.SetResult(null);
                        }
                    }
                }
                else
                {
                    tcs.SetResult(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing task dialog");
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        return tcs.Task;
    }

    private static string GetClientDisplayText(DiscoveredClient client)
    {
        var response = client.HelloResponse;
        var displayText = "";

        if (response.ClientState != null)
        {
            displayText += $"{response.ClientState}";
        }
        else
        {
            displayText += "Unidentified Client";

            if (response.ProcessId != null)
            {
                displayText += $" (PID: {response.ProcessId})";
            }
        }

        if (response.ProcessStartTime != null)
        {
            var startedAgo = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(response.ProcessStartTime.Value);
            displayText += $" - Started {startedAgo.ToFriendlyString()}";
        }

        return displayText;
    }

    private static void ShowErrorDialog(string title, string message, TaskDialogIcon? icon = null)
    {
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                TaskDialog.ShowDialog(new TaskDialogPage
                {
                    Caption = "XIVLauncher Link Handler",
                    Heading = title,
                    Text = message,
                    Icon = icon ?? TaskDialogIcon.Error,
                    Buttons = { TaskDialogButton.OK }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing error dialog");
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
