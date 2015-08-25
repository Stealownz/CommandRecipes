using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace CommandRecipes {
  public class Utils {
    public static List<RecPlayer> GetPlayerList(string name) {
      foreach (RecPlayer player in CommandRecipes.RPlayers) {
        if (player.name.ToLower().Contains(name.ToLower())) {
          return new List<RecPlayer>() { player };
        }
      }
      return new List<RecPlayer>();
    }

    public static RecPlayer GetPlayer(int index) {
      foreach (RecPlayer player in CommandRecipes.RPlayers)
        if (player.Index == index)
          return player;

      return null;
    }

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
        lActIngs.Add(String.Join(" or ", lGrIng));
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
      var player = Utils.GetPlayer(tsplr.Index);

      if (player.activeRecipe == null)
        return;

      List<string> inglist = Utils.ListIngredients(player.activeRecipe.ingredients);
      tsplr.SendInfoMessage("The {0} recipe requires: ", player.activeRecipe.name);
      tsplr.SendMessage(string.Format("Ingredients: {0}", String.Join(", ", inglist.ToArray(), 0, inglist.Count)), Color.LightGray);
      tsplr.SendInfoMessage("Type \"/craft -confirm\" to craft., or \"/craft -quit\" to quit");
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

    #region SetUpConfig
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
    #endregion

    #region GetPrefixById
    // Required until everyone gets their TShock updated
    public static string GetPrefixById(int id) {
      return id < 1 || id > 83 ? "" : Lang.prefix[id] ?? "";
    }
    #endregion

    #region FormatItem
    // Though it would be an interesting addition
    public static string FormatItem(Item item, int stacks = 0) {
      string prefix = GetPrefixById(item.prefix);
      if (prefix != "") {
        prefix += " ";
      }
      return String.Format("{0} {1}{2}",
        (stacks == 0) ? Math.Abs(item.stack) : stacks,
        prefix,
        item.name);
    }
    public static string LogFormatItem(Item item, int stacks = 0) {
      string str = GetPrefixById(item.prefix);
      string prefix = str == "" ? "" : "[" + str + "] ";
      return String.Format("{0} {1}{2}",
        (stacks == 0) ? Math.Abs(item.stack) : stacks,
        prefix,
        item.name);
    }
    #endregion

    #region AddToPrefixes(old)
    //public static void AddToPrefixes()
    //{
    //	#region Prefixes
    //	CommandRecipes.prefixes.Add(1, "Large");
    //	CommandRecipes.prefixes.Add(2, "Massive");
    //	CommandRecipes.prefixes.Add(3, "Dangerous");
    //	CommandRecipes.prefixes.Add(4, "Savage");
    //	CommandRecipes.prefixes.Add(5, "Sharp");
    //	CommandRecipes.prefixes.Add(6, "Pointy");
    //	CommandRecipes.prefixes.Add(7, "Tiny");
    //	CommandRecipes.prefixes.Add(8, "Terrible");
    //	CommandRecipes.prefixes.Add(9, "Small");
    //	CommandRecipes.prefixes.Add(10, "Dull");
    //	CommandRecipes.prefixes.Add(11, "Unhappy");
    //	CommandRecipes.prefixes.Add(12, "Bulky");
    //	CommandRecipes.prefixes.Add(13, "Shameful");
    //	CommandRecipes.prefixes.Add(14, "Heavy");
    //	CommandRecipes.prefixes.Add(15, "Light");
    //	CommandRecipes.prefixes.Add(16, "Sighted");
    //	CommandRecipes.prefixes.Add(17, "Rapid");
    //	CommandRecipes.prefixes.Add(18, "Hasty");
    //	CommandRecipes.prefixes.Add(19, "Intimidating");
    //	CommandRecipes.prefixes.Add(20, "Deadly");
    //	CommandRecipes.prefixes.Add(21, "Staunch");
    //	CommandRecipes.prefixes.Add(22, "Awful");
    //	CommandRecipes.prefixes.Add(23, "Lethargic");
    //	CommandRecipes.prefixes.Add(24, "Awkward");
    //	CommandRecipes.prefixes.Add(25, "Powerful");
    //	CommandRecipes.prefixes.Add(26, "Mystic");
    //	CommandRecipes.prefixes.Add(27, "Adept");
    //	CommandRecipes.prefixes.Add(28, "Masterful");
    //	CommandRecipes.prefixes.Add(29, "Inept");
    //	CommandRecipes.prefixes.Add(30, "Ignorant");
    //	CommandRecipes.prefixes.Add(31, "Deranged");
    //	CommandRecipes.prefixes.Add(32, "Intense");
    //	CommandRecipes.prefixes.Add(33, "Taboo");
    //	CommandRecipes.prefixes.Add(34, "Celestial");
    //	CommandRecipes.prefixes.Add(35, "Furious");
    //	CommandRecipes.prefixes.Add(36, "Keen");
    //	CommandRecipes.prefixes.Add(37, "Superior");
    //	CommandRecipes.prefixes.Add(38, "Forceful");
    //	CommandRecipes.prefixes.Add(39, "Broken");
    //	CommandRecipes.prefixes.Add(40, "Damaged");
    //	CommandRecipes.prefixes.Add(41, "Shoddy");
    //	CommandRecipes.prefixes.Add(42, "Quick");
    //	CommandRecipes.prefixes.Add(43, "Deadly");
    //	CommandRecipes.prefixes.Add(44, "Agile");
    //	CommandRecipes.prefixes.Add(45, "Nimble");
    //	CommandRecipes.prefixes.Add(46, "Murderous");
    //	CommandRecipes.prefixes.Add(47, "Slow");
    //	CommandRecipes.prefixes.Add(48, "Sluggish");
    //	CommandRecipes.prefixes.Add(49, "Lazy");
    //	CommandRecipes.prefixes.Add(50, "Annoying");
    //	CommandRecipes.prefixes.Add(51, "Nasty");
    //	CommandRecipes.prefixes.Add(52, "Manic");
    //	CommandRecipes.prefixes.Add(53, "Hurtful");
    //	CommandRecipes.prefixes.Add(54, "Strong");
    //	CommandRecipes.prefixes.Add(55, "Unpleasant");
    //	CommandRecipes.prefixes.Add(56, "Weak");
    //	CommandRecipes.prefixes.Add(57, "Ruthless");
    //	CommandRecipes.prefixes.Add(58, "Frenzying");
    //	CommandRecipes.prefixes.Add(59, "Godly");
    //	CommandRecipes.prefixes.Add(60, "Demonic");
    //	CommandRecipes.prefixes.Add(61, "Zealous");
    //	CommandRecipes.prefixes.Add(62, "Hard");
    //	CommandRecipes.prefixes.Add(63, "Guarding");
    //	CommandRecipes.prefixes.Add(64, "Armored");
    //	CommandRecipes.prefixes.Add(65, "Warding");
    //	CommandRecipes.prefixes.Add(66, "Arcane");
    //	CommandRecipes.prefixes.Add(67, "Precise");
    //	CommandRecipes.prefixes.Add(68, "Lucky");
    //	CommandRecipes.prefixes.Add(69, "Jagged");
    //	CommandRecipes.prefixes.Add(70, "Spiked");
    //	CommandRecipes.prefixes.Add(71, "Angry");
    //	CommandRecipes.prefixes.Add(72, "Menacing");
    //	CommandRecipes.prefixes.Add(73, "Brisk");
    //	CommandRecipes.prefixes.Add(74, "Fleeting");
    //	CommandRecipes.prefixes.Add(75, "Hasty");
    //	CommandRecipes.prefixes.Add(76, "Quick");
    //	CommandRecipes.prefixes.Add(77, "Wild");
    //	CommandRecipes.prefixes.Add(78, "Rash");
    //	CommandRecipes.prefixes.Add(79, "Intrepid");
    //	CommandRecipes.prefixes.Add(80, "Violent");
    //	CommandRecipes.prefixes.Add(81, "Legendary");
    //	CommandRecipes.prefixes.Add(82, "Unreal");
    //	CommandRecipes.prefixes.Add(83, "Mythical");
    //	#endregion
    //}
    #endregion
  }
}
