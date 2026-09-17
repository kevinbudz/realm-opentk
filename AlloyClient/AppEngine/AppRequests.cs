using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AlloyClient.Data;

namespace AlloyClient.AppEngine;

public struct AppResponse {
    public bool Success;
    public string Message;
}

public static class AppRequests {

    public static async Task<AppResponse> Startup() {
        var login = GlobalData.Get<LoginData>();

        // An empty local account is the Flash client's guest path. Guests do
        // not pass /account/verify, but /char/list still returns the guest
        // account and its character list.
        if (login is null || (string.IsNullOrWhiteSpace(login.Username) && string.IsNullOrWhiteSpace(login.Password))) {
            return await GetCharList();
        }

        // VerifyAsync loads the list after a successful verify. Returning its
        // result keeps startup to one character-list request and propagates a
        // list failure to the loading route.
        return await VerifyAsync(login.Username, login.Password);
    }
    
    public static async Task<AppResponse> VerifyAsync() {
        var login = GlobalData.Get<LoginData>() ?? LoginData.Default;
        return await VerifyAsync(login.Username, login.Password);
    }

    public static async Task<AppResponse> VerifyAsync(string username, string password, bool saveInfo = false) {
        var response = await AppEngineClient.SendRequest("/account/verify", BuildAccountRequestData(username, password), 3);
        
        if (response == null) {
            return new AppResponse { Success = false, Message = "Failed to contact server." };
        }

        XElement xml;
        try {
            xml = XElement.Parse(response);
        } catch (XmlException) {
            return new AppResponse {
                Success = false,
                Message = string.IsNullOrWhiteSpace(response) ? "Invalid server response." : response.Trim()
            };
        }

        if (xml.Name.LocalName == "Error") {
            return new AppResponse {
                Success = false,
                Message = string.IsNullOrWhiteSpace(xml.Value) ? "Unable to sign in." : xml.Value.Trim()
            };
        }

        //realm-server /account/verify answers bare <Success/> (no account
        //body), so there is nothing to parse here. Account/character data
        //arrives via /char/list below.
        GlobalData.Add(new LoginData(username, password));

        if (saveInfo) {
            Settings.SaveLocalAccount();
        }

        var charList = await GetCharList();
        if (!charList.Success) {
            return charList;
        }

        return new AppResponse { Success = true };
    }
    
    public static async Task<AppResponse> Register(string username, string password) {
        var data = new Dictionary<string, string> {{"newUsername", username}, {"newPassword", password}};
        var request = await AppEngineClient.SendRequest("/account/register", data, 3);
        
        if (request == null) {
            return new AppResponse { Success = false, Message = "Failed to contact server." };
        }

        var result = XElement.Parse(request).Value;

        if (result != string.Empty) {
            return new AppResponse { Success = false, Message = result };
        }

        return await VerifyAsync(username, password, true);
    }
    
    public static async Task<AppResponse> PurchaseCharSlot() {
        var login = GlobalData.Get<LoginData>();

        if (login is null) {
            return new AppResponse { Success = false, Message = "Not logged in" };
        }
        var response = await AppEngineClient.SendRequest("/account/purchaseCharSlot", BuildAccountRequestData(login.Username, login.Password), 0);
        
        if (response == null) {
            return new AppResponse { Success = false, Message = "Failed to contact server." };
        }

        var result = XElement.Parse(response).Value;
        
        return result == string.Empty ? new AppResponse { Success = true } : new AppResponse { Success = false, Message = result };
    }
    
    public static async Task<AppResponse> DeleteCharacter(int characterId) {
        var login = GlobalData.Get<LoginData>();
        if (login == null) return new AppResponse { Message = "Not logged in" };
        var data = BuildAccountRequestData(login.Username, login.Password);
        data["charId"] = characterId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = await AppEngineClient.SendRequest("/char/delete", data, 1);
        if (response == null) return new AppResponse { Message = "Failed to contact server." };
        try {
            var xml = XElement.Parse(response);
            return new AppResponse { Success = xml.Name.LocalName == "Success", Message = xml.Value };
        } catch (XmlException) {
            return new AppResponse { Message = "Invalid server response." };
        }
    }

    public static async Task<XElement> GetCharacterFame(int accountId, int characterId) {
        var response = await AppEngineClient.SendRequest("/char/fame", new Dictionary<string, string> {
            ["accountId"] = accountId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["charId"] = characterId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }, 3);
        if (response == null) return new XElement("Error", "Failed to contact server.");
        try {
            return XElement.Parse(response);
        } catch (XmlException) {
            return new XElement("Error", "Invalid server response.");
        }
    }

    public static async Task<AppResponse> GetCharList() {
        var login = GlobalData.Get<LoginData>() ?? LoginData.Default;
        var response = await AppEngineClient.SendRequest("/char/list", BuildAccountRequestData(login.Username, login.Password), 3);
        
        if (response == null) {
            return new AppResponse { Success = false, Message = "Failed to contact server." };
        }
        
        try {
            var xml = XElement.Parse(response);

            if (xml.Name.LocalName == "Error" || xml.Element("Account") == null) {
                var message = xml.Value;
                return new AppResponse { Success = false, Message = string.IsNullOrWhiteSpace(message) ? "Failed to load character list." : message };
            }

            // Build all data before publishing any of it. A malformed list
            // must not leave a partial account state behind for a later route.
            var account = new AccountData(xml.Element("Account"));
            var characterList = new CharacterListData(xml);
            var news = NewsData.FromCharacterList(xml);
            var servers = new ServerListData(xml.Element("Servers") ?? DefaultServersXml());

            GlobalData.Add(account);
            GlobalData.Add(characterList);
            GlobalData.Add(news);
            GlobalData.Add(servers);
        } catch (Exception) {
            return new AppResponse { Success = false, Message = "Invalid character list response." };
        }
        
        return new AppResponse{ Success = true };
    }
    
    //realm-server /char/list carries no Servers element, so fall back to the
    //configured game endpoint instead of crashing on a missing element.
    private static XElement DefaultServersXml() => new("Servers",
        new XElement("Server",
            new XElement("Name", "Default"),
            new XElement("DNS", Settings.GameServerAddress),
            new XElement("Port", Settings.GameServerPort)));

    private static Dictionary<string, string> BuildAccountRequestData(string username, string password) => new() {{"username", username}, {"password", password}};
}
