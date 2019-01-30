# MsTeamsChannelProvider Readme

## General notes:
- The main class that implements IExternalChannelProvider is **MsTeamsChannelProvider**. It is in *_Siemplify* folder inside GraphApi.Web project.
- MsTeamsChannelProvider also implements *IExternalChannelProviderAsync* interface that duplicates methods from IExternalChannelProvider with Async postfix. You could remove synchronous methods from class if not needed.
- Base project for MsTeamsChannelProvider implementation is GraphApi.Web sample from here https://github.com/microsoftgraph/csharp-teams-sample-graph.
- To test project properly you have to create application in [Azure Active Directory](https://portal.azure.com). You may use that [guide](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal) [](https://github.com/microsoftgraph/csharp-teams-sample-graph/blob/master/README.md)as a starting point (if not yet). You also need to give application Read/Write access for Users and Groups in Graph API category (add it to app in Azure AD).
- Then you need to obtain AppId and obtain encrypted password from Azure AD App registration. And then create Web.config.secrets file and paste AppId and 
  AppSecret (encrypted) there.
- The test page is http://localhost:55069/ allows you to test/trace all features of MsTeamsChannelProvider class. On first run and further it will redirect you to Microsoft Login Page. You have to login with account that could manage O365 accounts and which has access to Azure Active Directory App.


## Notes regarding MsTeamsChannelProvider class:
- MsTeamsChannelProvider.*CurrentTeamId* - this property have to be set before any call. By default it sets to first team on Connect() call. There is also *SelectFirstTeam()* method that assigns CurrentTeamId to first team.
- Actually, MS Teams architecture implies to manipulate *teams* and users, instead of channels (like in Slack).
- *GetChannelUsers*=*GetAllUsers*: because all users in team has access to any channel
- *AddUserToChannel*(teamName, displayUserName) - will add user to team and she will have access to all channels at once. If user already in team then exception occurred.
- *RemoveUserFromChannel*(teamName, displayUserName) - removes user from team. If user not in team then exception occurred.
- *CreateChannel*(name, userList): will not process user list. As users that already in team will have access to channel. Use AddUserToChannel call which adds existing Office 365 users to the team.
- I left 'Channel' postfix for compatibility with IExternalChannelProvider interface. Actually, XyzChannel calls will manipulate *teams*, not *channels*: AddUserToChannel, RemoveUserFromChannel, GetChannelUsers. Exceptions are CloseChannel and CreateChannel which does what it means.


## Recommendations:
- Use async methods of IExternalChannelProviderAsync to avoid additional calls for synchronous methods. I.e., for instance, use 'await GetAllUsersAsync()' instead of GetAllUsers.
- If you don't need synchronous methods then it may be removed from code at all. The reason of using async methods is that most of internal calls of library classes are async as well (e.g to access token and Graph API).
- In addition, synchonous methods are just a wrappers arround async methods.
- Rename XyzChannel(channelName, userName) to XyzTeam(teamName, userName). E.g., AddUserToChannel -> AddUserToTeam
- This is specific for MS Teams, but such naming is not confusing.
- Pass teamName or even better teamId to methods instead of using implicit property CurrentTeamId
## Future improvements:
- As I used already existed project to save time, then the main class MsTeamsExternalProvider may be refactored and trimmed from unnecessary dependencies of that project, like *FormOutput* class, which is uses to show test application results. Some of the Models classes are not need in MsTeamsExternalProvider class now.
- In general, MsTeamsExternalProvider class is a wrapper of existed GraphService class. This may be refactored too.
- ChannelUser.Picture isn't filled for now. We may use this API call https://docs.microsoft.com/en-us/graph/api/profilephoto-get?view=graph-rest-1.0 . It returns 
  the *binary data* of the requested photo, not string or url.
- Namespaces needs refactoring
- Login to Microsoft account probably may be moved to Connect() method. For now its in *Startup.Auth.cs*.


## Links:
- https://github.com/microsoftgraph/csharp-teams-sample-graph - base project for MsTeamsChannelProvider
- https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-v2-netcore-daemon - example of simple console MS teams app
