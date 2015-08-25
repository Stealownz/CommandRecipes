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
    public static List<string> cats = new List<string>();
    public static List<string> recs = new List<string>();
    public static List<RecPlayer> RPlayers = new List<RecPlayer>();
    //public static Dictionary<int, string> prefixes = new Dictionary<int, string>();
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
      Commands.ChatCommands.Add(new Command("cmdrec.admin.reload", RecReload, "craftreloadcfg","craftreload","recrld") {
        HelpText = "Reloads AllRecipes.json"
      });

      //Utils.AddToPrefixes();
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
      if (args.Parameters.Count == 0) {
        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /craft <recipe/-quit/-list/-allcats/-cat/-confirm>");
        return;
      }

      var subcmd = args.Parameters[0].ToLower();

      switch (subcmd) {
        case "-list":
          int page;
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out page))
            return;

          List<string> allRec = new List<string>();

          // Add any recipe that isn't invisible kappa
          foreach (Recipe rec in CommandRecipes.config.Recipes.FindAll(r => !r.invisible))
            allRec.Add(rec.name);
          PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(allRec),
            new PaginationTools.Settings {
              HeaderFormat = "Recipes ({0}/{1}):",
              FooterFormat = "Type /craft -list {0} for more.",
              NothingToDisplayString = "There are currently no recipes defined!"
            });
          return;
        case "-allcats":
          int pge;
          if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pge))
            return;

          List<string> allCat = new List<string>();

          // Another ditto from -list
          foreach (Recipe rec in CommandRecipes.config.Recipes.FindAll(r => !r.invisible))
            rec.categories.ForEach(i => {
              if (!allCat.Contains(i))
                allCat.Add(i);
            });
          PaginationTools.SendPage(args.Player, 1, PaginationTools.BuildLinesFromTerms(allCat),
            new PaginationTools.Settings {
              HeaderFormat = "Recipe Categories ({0}/{1}):",
              FooterFormat = "Type /craft -cat {0} for more.",
              NothingToDisplayString = "There are currently no categories defined!"
            });
          return;
        case "-cat":
          if (args.Parameters.Count < 2) {
            args.Player.SendErrorMessage("Invalid category!");
            return;
          }

          args.Parameters.RemoveAt(0);
          string cat = string.Join(" ", args.Parameters);
          if (!cats.Contains(cat.ToLower())) {
            args.Player.SendErrorMessage("Invalid category!");
            return;
          }
          else {
            List<string> catrec = new List<string>();

            // Keep bringing them!
            foreach (Recipe rec in config.Recipes.FindAll(r => !r.invisible)) {
              rec.categories.ForEach(i => {
                if (cat.ToLower() == i.ToLower())
                  catrec.Add(rec.name);
              });
            }
            args.Player.SendInfoMessage("Recipes in this category:");
            args.Player.SendInfoMessage("{0}", String.Join(", ", catrec));
          }
          return;
        case "-quit":
          args.Player.SendInfoMessage("Returning dropped items...");
          foreach (RecItem itm in player.droppedItems) {
            item = new Item();
            item.SetDefaults(itm.name);
            args.Player.GiveItem(item.type, itm.name, item.width, item.height, itm.stack, itm.prefix);
            player.TSPlayer.SendInfoMessage("Returned {0}.", Utils.FormatItem((Item)itm));
          }
          player.activeRecipe = null;
          player.droppedItems.Clear();
          player.TSPlayer.SendInfoMessage("Successfully quit crafting.");
          return;
        case "-confirm":
          int count = 0;
          Dictionary<int, bool> finishedGroup = new Dictionary<int, bool>();
          Dictionary<int, int> slots = new Dictionary<int, int>();
          int ingredientCount = player.activeIngredients.Count;
          foreach (Ingredient ing in player.activeIngredients) {
            if (!finishedGroup.ContainsKey(ing.group)) {
              finishedGroup.Add(ing.group, false);
            }
            else if (ing.group != 0)
              ingredientCount--;
          }
          foreach (Ingredient ing in player.activeIngredients) {
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
                count++;
              }
            }
          }
          if (count < ingredientCount) {
            args.Player.SendErrorMessage("Insufficient ingredients!");
            return;
          }
          if (!args.Player.InventorySlotAvailable) {
            args.Player.SendErrorMessage("Insufficient inventory space!");
            return;
          }
          foreach (var slot in slots) {
            item = args.TPlayer.inventory[slot.Key];
            var ing = player.activeIngredients.GetIngredient(item.name, item.prefix);
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
        default:
          if (player.activeRecipe != null) {
            args.Player.SendErrorMessage("You must finish crafting or quit your current recipe!");
            return;
          }
          string str = string.Join(" ", args.Parameters);
          if (!recs.Contains(str.ToLower())) {
            args.Player.SendErrorMessage("Invalid recipe!");
            return;
          }
          foreach (Recipe rec in config.Recipes) {
            if (str.ToLower() == rec.name.ToLower()) {
              if (!rec.permissions.Contains("") && !args.Player.Group.CheckPermissions(rec.permissions)) {
                args.Player.SendErrorMessage("You do not have the required permission to craft the recipe: {0}!", rec.name);
                return;
              }
              if (!Utils.CheckIfInRegion(args.Player, rec.regions)) {
                args.Player.SendErrorMessage("You are not in a valid region to craft the recipe: {0}!", rec.name);
                return;
              }
              player.activeIngredients = new List<Ingredient>(rec.ingredients.Count);
              rec.ingredients.ForEach(i => {
                player.activeIngredients.Add(i.Clone());
              });
              player.activeRecipe = rec.Clone();
              break;
            }
          }
          if (player.activeRecipe != null) {
            List<string> inglist = Utils.ListIngredients(player.activeRecipe.ingredients);
            args.Player.SendInfoMessage("The {0} recipe requires {1} to craft. Type \"/craft -confirm\" to craft.",
              player.activeRecipe.name,
              (inglist.Count > 1) ? String.Join(", ", inglist.ToArray(), 0, inglist.Count - 1) + ", and " + inglist.LastOrDefault() : inglist[0]);
          }
          break;
      }
    }

    public static void RecReload(CommandArgs args) {
      Utils.SetUpConfig();
      args.Player.SendInfoMessage("Attempted to reload the config file");
    }
    #endregion
  }
}
