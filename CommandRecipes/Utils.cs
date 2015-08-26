using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

using Wolfje.Plugins.SEconomy;

namespace CommandRecipes {
  public static class Utils {
    public static List<string> ListIngredients(List<Ingredient> actIngs) {
      List<string> lActIngs = new List<string>();
      List<int> groups = new List<int>();
      foreach (Ingredient item in actIngs)
        if (item.group == 0)
          lActIngs.Add(FormatItem((Item)item));
        else if (!groups.Contains(item.group))
          groups.Add(item.group);

      for (int i = 0; i < groups.Count; i++) {
        List<string> lGrIng = new List<string>();
        foreach (Ingredient item in actIngs) {
          if (groups[i] == item.group)
            lGrIng.Add(FormatItem((Item)item));
        }
        lActIngs.Add("<" + String.Join(" OR ", lGrIng) + ">");
      }
      return lActIngs;
    }

    public static List<Product> DetermineProducts(List<Product> actPros) {
      List<Product> lActPros = new List<Product>();
      List<int> groups = new List<int>();
      foreach (Product pro in actPros) {
        if (pro.group == 0)
          lActPros.Add(pro);
        else if (!groups.Contains(pro.group))
          groups.Add(pro.group);
      }

      for (int i = 0; i < groups.Count; i++) {
        List<Product> proPool = new List<Product>();
        foreach (Product pro in actPros) {
          if (groups[i] == pro.group)
            proPool.Add(pro);
        }

        Random r = new Random();
        double diceRoll = r.Next(100);

        int cumulative = 0;
        for (int j = 0; j < proPool.Count; j++) {
          cumulative += (proPool[j].weight);
          if (diceRoll < cumulative) {
            lActPros.Add(proPool[j]);
            break;
          }
        }
      }
      return lActPros;
    }

    public static bool CheckIfInRegion(TSPlayer plr, List<string> region) {
      if (region.Contains(""))
        return true;

      Region r;
      foreach (var reg in region) {
        r = TShock.Regions.GetRegionByName(reg);
        if (r != null && r.InArea((int)plr.X, (int)plr.Y))
          return true;
      }
      return false;
    }

    public static void PrintCurrentRecipe(TSPlayer tsplr) {
      var player = CommandRecipes.RPlayers[tsplr.Index];

      if (player.activeRecipe == null)
        return;

      List<string> inglist = Utils.ListIngredients(player.activeRecipe.ingredients);
      tsplr.SendInfoMessage("The {0} recipe requires: ", string.Concat("[i:", player.activeRecipe.name, "]"));
      tsplr.SendMessage(string.Format("Ingredients: {0}", String.Join(", ", inglist.ToArray(), 0, inglist.Count)), Color.LightGray);
      if (SEconomyPlugin.Instance != null) {
        string money = SEconomyPlugin.Instance.Configuration.MoneyConfiguration.MoneyName;
        tsplr.SendMessage(string.Format("Cost: {0} {1}", player.activeRecipe.SEconomyCost, money), Color.LightGray);
      }
      tsplr.SendInfoMessage("Type \"/craft -confirm\" to craft, or \"/craft -quit\" to quit");
    }

    // I stole this code from my AutoRank plugin - with a few changes. It worked, so.
    public static string ParseCommand(TSPlayer player, string text) {
      if (player == null || string.IsNullOrEmpty(text))
        return "";

      var replacements = new Dictionary<string, object>();

      replacements.Add("$group", player.Group.Name);
      replacements.Add("$ip", player.IP);
      replacements.Add("$playername", player.Name);
      replacements.Add("$username", player.User.Name);

      foreach (var word in replacements) {
        // Quotes are automatically added - no more self-imposed quotes with $playername!
        text = text.Replace(word.Key, "\"{0}\"".SFormat(word.Value.ToString()));
      }

      return text;
    }

    // ^ Such ditto, many IVs.
    public static List<string> ParseParameters(string text) {
      text = text.Trim();
      var args = new List<string>();
      StringBuilder sb = new StringBuilder();
      bool quote = false;
      for (int i = 0; i < text.Length; i++) {
        char c = text[i];

        if (char.IsWhiteSpace(c) && !quote) {
          args.Add(sb.ToString());
          sb.Clear();
        }
        else if (c == '"') {
          quote = !quote;
        }
        else {
          sb.Append(c);
        }
      }
      args.Add(sb.ToString());
      return args;
    }
    
    public static void SetUpConfig() {
      try {
        if (!Directory.Exists(CommandRecipes.configDir))
          Directory.CreateDirectory(CommandRecipes.configDir);

        CommandRecipes.config = RecConfig.Read();
      }
      catch (Exception ex) {
        TShock.Log.ConsoleError("Error in recConfig.json!");
        TShock.Log.ConsoleError(ex.ToString());
      }
    }
    
    public static string FormatItem(Item item, int stacks = 0) {
      string str = TShock.Utils.GetPrefixById(item.prefix);
      string prefix = str == "" ? "" : "[" + str + "] ";
      if (prefix == "") {
        return string.Format("[i/s{0}:{1}]", (stacks == 0) ? Math.Abs(item.stack) : stacks, item.name);
      } else {
        return string.Format("[i/p{0}:{1}]", prefix, item.name);
      }
    }
  }
}
