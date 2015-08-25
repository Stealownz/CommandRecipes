using System;
using System.IO;
using System.IO.Streams;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.DB;

namespace CommandRecipes {
  [ApiVersion(1, 21)]
  public class CommandRecipes : TerrariaPlugin {
    public static List<RecPlayer> RPlayers = new List<RecPlayer>();
    public static RecConfig config { get; set; }
    public static string configDir { get { return Path.Combine(TShock.SavePath, "CommandRecipes"); } }
    public static string configPath { get { return Path.Combine(configDir, "AllRecipes.json"); } }
    public RecipeLog Log { get; set; }

    #region Info
    public override string Name {
      get { return "CommandRecipes"; }
    }

    public override string Author {
      get { return "aMoka and Enerdy"; }
    }

    public override string Description {
      get { return "Recipes through commands and chat."; }
    }

    public override Version Version {
      get { return Assembly.GetExecutingAssembly().GetName().Version; }
    }

    public CommandRecipes(Main game)
      : base(game) {
      Order = -10;

      config = new RecConfig();
      Log = new RecipeLog();
    }
    #endregion

    #region Plugin overrides
    public override void Initialize() {
      ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
      ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
      PlayerHooks.PlayerPostLogin += OnPostLogin;
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
        ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
        PlayerHooks.PlayerPostLogin -= OnPostLogin;
        Log.Dispose();
      }
      base.Dispose(disposing);
    }
    #endregion

    #region Plugin Hooks
    void OnPostInitialize(EventArgs args) {
      Commands.ChatCommands.Add(new Command("cmdrec.player.craft", Craft, "craft") {
        HelpText = "Allows the player to craft items via command from config-defined recipes."
      });
      Commands.ChatCommands.Add(new Command("cmdrec.admin.reload", RecReload, "craftreloadcfg", "craftreload", "recrld") {
        HelpText = "Reloads AllRecipes.json"
      });

      Utils.SetUpConfig();
      Log.Initialize();
    }

    void OnPostLogin(PlayerPostLoginEventArgs args) {
      RPlayers.Add(new RecPlayer(args.Player.Index));
      var RecPlayer = RPlayers.AddToList(new RecPlayer(args.Player.Index));
    }

    void OnLeave(LeaveEventArgs args) {
      var player = Utils.GetPlayer(args.Who);

      RPlayers.RemoveAll(pl => pl.Index == args.Who);
    }
    #endregion

    #region Commands
    void Craft(CommandArgs args) {
      Item item;
      var player = Utils.GetPlayer(args.Player.Index);
      int page = 1;

      if (args.Parameters.Count == 0) {
        if (player.activeRecipe != null) {
          Utils.PrintCurrentRecipe(args.Player);
          return;
        }
        else {
          args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /craft <recipe/-quit/-list/-allcats/-cat/-confirm>");
          return;
        }
      }

      switch (args.Parameters[0].ToLowerInvariant()) {
        #region -list
        case "-list":
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out page))
            return;

          List<string> allRec = CommandRecipes.config.Recipes.Where(r => !r.invisible).Select(r => r.name).ToList();

          PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(allRec),
            new PaginationTools.Settings {
              HeaderFormat = "All Recipes ({0}/{1}):",
              FooterFormat = "Type /craft -list {0} for more.",
              NothingToDisplayString = "There are currently no recipes defined!"
            });
          return;
        #endregion

        #region -allcats
        case "-allcats":
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out page))
            return;

          List<string> cats = config.Recipes.SelectMany(x => x.categories).Distinct().ToList();

          PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(cats),
            new PaginationTools.Settings {
              HeaderFormat = "Recipe Categories ({0}/{1}):",
              FooterFormat = "Type /craft -allcats {0} for more.",
              NothingToDisplayString = "There are currently no categories defined!"
            });
          return;
        #endregion

        #region -cat
        case "-cat":
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out page))
            return;

          if (args.Parameters.Count < 2) {
            args.Player.SendErrorMessage("Invalid category!");
            return;
          }

          args.Parameters.RemoveAt(0);
          string cat = string.Join(" ", args.Parameters);
          List<string> catrec = config.Recipes
            .Where(r => !r.invisible && r.categories.Contains(cat, StringComparer.InvariantCultureIgnoreCase))
            .Select(x => x.name).ToList();

          PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(catrec),
            new PaginationTools.Settings {
              HeaderFormat = "Recipes in this category ({0}/{1}):",
              FooterFormat = string.Format("Type /craft -cat {0} {{{0}}} for more.", cat),
              NothingToDisplayString = "There are currently no recipes in this category defined!"
            });
          return;
        #endregion

        #region -quit
        case "-quit":
          //args.Player.SendInfoMessage("Returning dropped items...");
          //foreach (RecItem itm in player.droppedItems) {
          //  item = new Item();
          //  item.SetDefaults(itm.name);
          //  args.Player.GiveItem(item.type, itm.name, item.width, item.height, itm.stack, itm.prefix);
          //  player.TSPlayer.SendInfoMessage("Returned {0}.", Utils.FormatItem((Item)itm));
          //}
          player.activeRecipe = null;
          player.TSPlayer.SendInfoMessage("Successfully quit crafting.");
          return;
        #endregion

        #region -confirm
        case "-confirm":
          int ingcount = 0;
          Dictionary<int, bool> finishedGroup = new Dictionary<int, bool>();
          Dictionary<int, int> slots = new Dictionary<int, int>();
          int ingredientCount = player.activeRecipe.ingredients.Count;
          foreach (Ingredient ing in player.activeRecipe.ingredients) {
            if (!finishedGroup.ContainsKey(ing.group)) {
              finishedGroup.Add(ing.group, false);
            }
            else if (ing.group != 0)
              ingredientCount--;
          }
          foreach (Ingredient ing in player.activeRecipe.ingredients) {
            if (ing.group == 0 || !finishedGroup[ing.group]) {
              Dictionary<int, RecItem> ingSlots = new Dictionary<int, RecItem>();
              for (int i = 58; i >= 0; i--) {
                item = args.TPlayer.inventory[i];
                if (ing.name == item.name && (ing.prefix == -1 || ing.prefix == item.prefix)) {
                  ingSlots.Add(i, new RecItem(item.name, item.stack, item.prefix));
                }
              }
              if (ingSlots.Count == 0)
                continue;

              int totalStack = 0;
              foreach (var key in ingSlots.Keys)
                totalStack += ingSlots[key].stack;

              if (totalStack >= ing.stack) {
                foreach (var key in ingSlots.Keys)
                  slots.Add(key, (ingSlots[key].stack < ing.stack) ? args.TPlayer.inventory[key].stack : ing.stack);
                if (ing.group != 0)
                  finishedGroup[ing.group] = true;
                ingcount++;
              }
            }
          }

          if (ingcount < ingredientCount) {
            args.Player.SendErrorMessage("Insufficient ingredients!");
            return;
          }

          if (!args.Player.InventorySlotAvailable) {
            args.Player.SendErrorMessage("Insufficient inventory space!");
            return;
          }
          foreach (var slot in slots) {
            item = args.TPlayer.inventory[slot.Key];
            var ing = player.activeRecipe.ingredients.GetIngredient(item.name, item.prefix);
            if (ing.stack > 0) {
              int stack;
              if (ing.stack < slot.Value)
                stack = ing.stack;
              else
                stack = slot.Value;

              item.stack -= stack;
              ing.stack -= stack;
              NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, "", args.Player.Index, slot.Key);
              if (!player.droppedItems.ContainsItem(item.name, item.prefix))
                player.droppedItems.Add(new RecItem(item.name, stack, item.prefix));
              else
                player.droppedItems.GetItem(item.name, item.prefix).stack += slot.Value;
            }
          }
          List<Product> lDetPros = Utils.DetermineProducts(player.activeRecipe.products);
          foreach (Product pro in lDetPros) {
            Item product = new Item();
            product.SetDefaults(pro.name);
            product.Prefix(pro.prefix);
            pro.prefix = product.prefix;
            player.TSPlayer.GiveItem(product.type, product.name, product.width, product.height, pro.stack, product.prefix);
            player.TSPlayer.SendSuccessMessage("Received {0}.", Utils.FormatItem((Item)pro));
          }
          List<RecItem> prods = new List<RecItem>();
          lDetPros.ForEach(i => prods.Add(new RecItem(i.name, i.stack, i.prefix)));
          Log.Recipe(new LogRecipe(player.activeRecipe.name, player.droppedItems, prods), player.name);
          player.activeRecipe = null;
          player.droppedItems.Clear();
          player.TSPlayer.SendInfoMessage("Finished crafting.");
          return;
        #endregion

        #region default
        default:
          if (!args.Player.IsLoggedIn) {
            args.Player.SendErrorMessage("You must be logged in to use this command!");
            return;
          }

          if (player.activeRecipe != null) {
            args.Player.SendErrorMessage("You must finish crafting or quit your current recipe!");
            return;
          }

          string recipe = string.Join(" ", args.Parameters);
          Recipe rec = config.Recipes.Where(r => r.name.ToLower() == recipe.ToLower()).FirstOrDefault();
          if (rec == null) {
            args.Player.SendErrorMessage("Invalid recipe!");
            return;
          }
          if (!rec.permissions.Contains("") && !args.Player.Group.CheckPermissions(rec.permissions)) {
            args.Player.SendErrorMessage("You do not have the required permission to craft the recipe: {0}!", rec.name);
            return;
          }
          if (!Utils.CheckIfInRegion(args.Player, rec.regions)) {
            args.Player.SendErrorMessage("You are not in a valid region to craft the recipe: {0}!", rec.name);
            return;
          }

          player.activeRecipe = rec.Clone();

          if (player.activeRecipe == null)
            return;

          Utils.PrintCurrentRecipe(args.Player);
          return;
          #endregion
      }
    }

    public static void RecReload(CommandArgs args) {
      Utils.SetUpConfig();
      args.Player.SendInfoMessage("Attempted to reload the config file");
    }
    #endregion
  }
}
