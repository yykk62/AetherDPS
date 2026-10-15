using System.Net;
using System.Text;

namespace AetherDPS.UiTests;

/// <summary>
/// Antwortet wie die öffentliche NCSOFT-API, aber mit erfundenen Daten – damit die Tests nie
/// ins Internet gehen. Jeder gesuchte Name wird „gefunden“.
/// </summary>
public sealed class FakeNcsoft : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests++;
        var url = request.RequestUri!.ToString();
        string json;
        if (url.Contains("/search/"))
        {
            var query = request.RequestUri.Query;
            var name = Uri.UnescapeDataString(query.Split("keyword=")[1].Split('&')[0]);
            json = $$"""{"list":[{"name":"<strong>{{name}}</strong>","serverId":1317,"characterId":"{{Uri.EscapeDataString(name)}}"}]}""";
        }
        else if (url.Contains("/equipment/item")) json = Item;
        else if (url.Contains("/equipment")) json = Equipment;
        else if (url.Contains("/info"))
        {
            var id = url.Split("characterId=")[1].Split('&')[0];
            json = Info.Replace("NAME", Uri.UnescapeDataString(id));
        }
        else return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    public const string Info = """
        {"profile":{"characterId":"x","characterLevel":45,"characterName":"NAME","className":"Waldläufer","combatPower":72435,
          "pcId":13,"profileImage":null,"raceName":"Elyos","serverId":1317,"serverName":"Hogalum"},
         "stat":{"statList":[
          {"name":"Macht","statSecondList":["Angriffskraft + +2.5%"],"type":"STR","value":25},
          {"name":"Agilität","statSecondList":["Ausweichen + +2.4%","Block + +2.4%"],"type":"DEX","value":24},
          {"name":"Gerechtigkeit [Nezekan]","statSecondList":["Verteidigung + +4.1%"],"type":"Justice","value":41},
          {"name":"Zeit [Siel]","statSecondList":["Kampftempo +4.1%"],"type":"Time","value":41},
          {"name":"Itemstufe","statSecondList":null,"type":"ItemLevel","value":1303}]},
         "title":{"ownedCount":148,"titleList":[
          {"equipCategory":"Attack","grade":"Legend","name":"Hüter des Tests","ownedCount":53,"totalCount":103},
          {"equipCategory":"Defense","grade":"Unique","name":"Wächter des Tests","ownedCount":45,"totalCount":95}]},
         "ranking":{"rankingList":null},
         "daevanion":{"boardList":[
          {"id":31,"name":"Nezekan","open":1,"openNodeCount":49,"openPercent":56,"totalNodeCount":88},
          {"id":32,"name":"Zikel","open":1,"openNodeCount":41,"openPercent":47,"totalNodeCount":88},
          {"id":36,"name":"Azphel","open":1,"openNodeCount":0,"openPercent":0,"totalNodeCount":152}]}}
        """;

    public const string Equipment = """
        {"equipment":{"equipmentList":[
          {"enchantLevel":10,"exceedLevel":0,"grade":"Unique","id":110430047,"name":"Übungsbogen","slotPos":1,"slotPosName":"MainHand"},
          {"enchantLevel":8,"exceedLevel":0,"grade":"Epic","id":2,"name":"Übungsharnisch","slotPos":3,"slotPosName":"Torso"},
          {"enchantLevel":6,"exceedLevel":0,"grade":"Legend","id":3,"name":"Übungshelm","slotPos":5,"slotPosName":"Helmet"},
          {"enchantLevel":0,"exceedLevel":0,"grade":"Rare","id":4,"name":"Übungsring","slotPos":24,"slotPosName":"Ring1"},
          {"enchantLevel":3,"exceedLevel":0,"grade":"Epic","id":5,"name":"Übungskette","slotPos":20,"slotPosName":"Necklace"}],"skinList":[]},
         "petwing":{"pet":{"id":1077,"level":1,"name":"Harcon"},"wing":{"enchantLevel":0,"grade":"Unique","id":1,"name":"Meisterhafte Daeva-Flügel"}},
         "skill":{"skillList":[
          {"category":"Active","name":"Scharfschuss","skillLevel":12},
          {"category":"Active","name":"Pfeilhagel","skillLevel":10},
          {"category":"Passive","name":"Adlerauge","skillLevel":5},
          {"category":"Dp","name":"Sturmpfeil","skillLevel":3},
          {"category":"Active","name":"Nicht gelernt","skillLevel":0}]}}
        """;

    public const string Item = """
        {"name":"Übungsbogen","grade":"Unique","level":45,"enchantLevel":10,"maxEnchantLevel":15,"categoryName":"Bogen","tradable":false,
         "magicStoneSlotCount":4,
         "mainStats":[{"name":"Angriffskraft","value":"223","extra":"50"},{"name":"Präzision","value":"100","extra":"0"}],
         "subStats":[{"name":"Macht","value":"15"},{"name":"Kritischer Treffer","value":"16"}],
         "magicStoneStat":[{"name":"LP","value":"+100","grade":"Legend"},{"name":"MP","value":"+50","grade":"Legend"}],
         "godStoneStat":[{"name":"Götterstein: Probe","desc":"Fügt bei Angriffen mit 10 % Chance 500 Windschaden zu."}]}
        """;
}
