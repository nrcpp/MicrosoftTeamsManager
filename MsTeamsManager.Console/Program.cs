using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using Siemplify.Common.ExternalChannels;
using Siemplify.Common.ExternalChannels.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MsTeamsManager
{
    class Program
    {
        static void Log(string msg) => Console.WriteLine(msg);

        static void SafeCall(Action call)
        {
            try
            {
                call();
            }
            catch (Exception ex)
            {
                Log("Exception: " + ex.Message);
            }
        }

        public static void Main()
        {
            string testChannel = "Test Channel " + Guid.NewGuid().ToString().Substring(0, 5);
            bool isChannelCreated = false;
            var msTeamsManager = new MsTeamsChannelProvider();

            try
            {                
                msTeamsManager.Connect();
                if (msTeamsManager.CurrentTeamId == null || msTeamsManager.Token == null)
                {
                    Log("No token or team to continue. Exit");
                    return;
                }
                
                var users = msTeamsManager.GetAllUsers();
                Log($"{users.Count} members found in team");
                if (users.Count < 2)
                {
                    Log("There are less than 2 users in team. Exit.");
                    return;
                }
            

                string testTeam = msTeamsManager.GetMyTeams().FirstOrDefault()?.displayName;
                if (testTeam == null)
                {
                    Log("No teams found. Exit");
                    return;
                }
                else
                {
                    if (msTeamsManager.SelectTeam(testTeam).Result)
                        Log($"{testTeam} - Team selected");
                }

                Log("Creating channel " + testChannel);

                msTeamsManager.CreateChannel(testChannel, null);
                isChannelCreated = true;

                var teamUsers = msTeamsManager.GetTeamUsers(testTeam);
                var buffer = $"Users in Team '{testTeam}':\r\n" + string.Join(", ", teamUsers.ConvertAll<string>(u => $"'{u.FullName}'"));
                Log(buffer);

                msTeamsManager.SendMessage(testChannel, buffer);


                var user = msTeamsManager.AddUserToTeam(testTeam, "dk@flatsolutions.onmicrosoft.com");      // TODO: add existed user's email
                if (user == null)                
                    Log($"User was not added to team");


                var msgs = msTeamsManager.GetMessages(testChannel);
                var messages = $"Messages from channel @{testChannel}:\r\n" + string.Join("\r\n", msgs.ConvertAll<string>(m => m.Text));

                // Uncomment to remove second user from selected team
                //Log($"RemoveUserFromTeam ({testTeam}, {users[1].FullName})");
                //msTeamsManager.RemoveUserFromTeam(testTeam, users[1].FullName);
            }

            catch (Exception ex)
            {
                Log("Error: \r\n" + ex.Message);
            }
            finally
            {                
                if (isChannelCreated)
                {
                    Console.WriteLine($"Press Enter to Close Channel '{testChannel}'");
                    Console.ReadLine();

                    SafeCall(() => msTeamsManager.CloseChannel(testChannel));       // remove previously created channel before 
                }

            }

            Console.WriteLine("Press Enter to exit");  Console.ReadLine();
        }
    }
}
