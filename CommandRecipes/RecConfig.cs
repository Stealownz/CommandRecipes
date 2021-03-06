﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace CommandRecipes {
  public class RecItem {
    public string name;
    public int stack;
    public int prefix;

    public RecItem(string name, int stack, int prefix = -1) {
      this.name = name;
      this.stack = stack;
      this.prefix = prefix;
    }

    public RecItem Clone() {
      return MemberwiseClone() as RecItem;
    }

    // Operators for explicit conversions
    public static explicit operator Item(RecItem item) {
      var titem = new Item();
      titem.SetDefaults(item.name);
      titem.stack = item.stack;
      titem.prefix = (byte)item.prefix;
      return titem;
    }

    public static explicit operator RecItem(Item item) {
      return new RecItem(item.name, item.stack, item.prefix);
    }
  }

  public class Ingredient {
    public string name;
    public int stack;
    public int prefix;
    public int group;

    public Ingredient(string name, int stack, int prefix, int group) {
      // Can now obtain the item from its netID. Yay!
      int id;
      if (int.TryParse(name, out id)) {
        Item item = TShock.Utils.GetItemById(id);
        this.name = item != null ? item.name : name;
      }
      else
        this.name = name;

      this.stack = stack;
      this.prefix = prefix;
      this.group = group;
    }

    public Ingredient Clone() {
      return MemberwiseClone() as Ingredient;
    }

    public static explicit operator Item(Ingredient item) {
      var titem = new Item();
      titem.SetDefaults(item.name);
      titem.stack = item.stack;
      titem.prefix = (byte)item.prefix;
      return titem;
    }
  }

  public class Product {
    public string name;
    public int stack;
    public int prefix;
    public int group;
    public int weight;

    public Product(string name, int stack, int prefix, int group, int weight) {
      // Ditto Ingredient
      int id;
      if (int.TryParse(name, out id)) {
        Item item = TShock.Utils.GetItemById(id);
        this.name = item != null ? item.name : name;
      }
      else
        this.name = name;

      this.stack = stack;
      this.prefix = prefix;
      this.group = group;
      this.weight = weight;
    }

    public Product Clone() {
      return MemberwiseClone() as Product;
    }

    public static explicit operator Item(Product item) {
      var titem = new Item();
      titem.SetDefaults(item.name);
      titem.stack = item.stack;
      titem.prefix = (byte)item.prefix;
      return titem;
    }
  }

  public abstract class RecipeFactory {
    public abstract Recipe Clone();
  }

  public class Recipe : RecipeFactory {
    public string name;
    public List<Ingredient> ingredients;
    public List<Product> products;
    public List<string> categories = new List<string>();
    public List<string> permissions = new List<string>();
    public List<string> regions = new List<string>();
    public bool invisible = false;
    public int SEconomyCost = 0;
    public string[] commands;

    public Recipe(string name, List<Ingredient> ingredients, List<Product> products, List<string> categories = null, List<string> permissions = null, List<string> regions = null, bool invisible = false, string[] commands = null) {
      this.name = name;
      this.ingredients = ingredients;
      this.products = products;
      this.categories = categories;
      this.permissions = permissions;
      this.regions = regions;
      this.invisible = invisible;
      this.SEconomyCost = SEconomyCost;
      this.commands = commands;
    }

    public override Recipe Clone() {
      var clone = MemberwiseClone() as Recipe;
      clone.ingredients = new List<Ingredient>(ingredients.Count);
      clone.products = new List<Product>(products.Count);
      ingredients.ForEach(i => clone.ingredients.Add(i.Clone()));
      products.ForEach(i => clone.products.Add(i.Clone()));
      return clone;
    }

    /// <summary>
    /// Runs associated commands. Returns -1 if an exception occured.
    /// </summary>
    public int ExecuteCommands(TSPlayer player) {
      int cmdCount = 0;
      try {
        if (commands == null || commands.Length < 1)
          return 0;

        List<string> args;
        Command cmd;
        string text;
        for (int i = 0; i < commands.Length; i++) {
          text = Utils.ParseCommand(player, commands[i]);
          text = text.Remove(0, 1);

          args = Utils.ParseParameters(text);
          cmd = Commands.ChatCommands.Find(c => c.HasAlias(args[0]));

          // Found the command, may remove its alias.
          args.RemoveAt(0);

          if (cmd != null) {
            try {
              // Execute the command without checking for permissions (?)
              cmd.CommandDelegate(new CommandArgs(text, player, args));
              cmdCount++;
            }
            catch (Exception) {
              // Swallow (and shall the rest conclude). Delicious.
            }
          }
        }
      }
      catch (Exception) {
        return -1;
      }

      return cmdCount;
    }
  }

  public class RecConfig {
    public List<Recipe> Recipes = new List<Recipe>();
    
    public static RecConfig Read() {
      if (!File.Exists(CommandRecipes.configPath)) {
        RecConfig res = new RecConfig();
        res.Recipes.Add(new Recipe("Copper Broadsword",
          new List<Ingredient>() {
            new Ingredient("Copper Bar", 8, 0, 1),
            new Ingredient("Iron Bar", 8, 0, 1),
            new Ingredient("Stone Block", 20, 0, 0),
            new Ingredient("Wooden Hammer", 1, 0, 0) },
          new List<Product>() {
            new Product("Copper Broadsword", 1, 41, 1, 50),
            new Product("Copper Shortsword", 1, 41, 1, 50),
            new Product("Wooden Hammer", 1, 39, 0, 100) },
          new List<string> { "Example" },
          new List<string> { "" },
          new List<string> { "" }));
        res.Recipes.Add(new Recipe("Iron Broadsword",
          new List<Ingredient>() {
            new Ingredient("Iron Bar", 8, 0, 0),
            new Ingredient("Stone Block", 20, 0, 0),
            new Ingredient("Wooden Hammer", 1, -1, 0) },
          new List<Product>() {
            new Product("Iron Broadsword", 1, 41, 0, 100),
            new Product("Wooden Hammer", 1, 39, 0, 100) },
          new List<string> { "Example", "Example2" },
          new List<string> { "cmdrec.craft.example" },
          new List<string> { "" }));
        Write(res);
        return res;
      }

      try {
        return Write(JsonConvert.DeserializeObject<RecConfig>(File.ReadAllText(CommandRecipes.configPath)));
      }
      catch (Exception ex) {
        TShock.Log.Error("[CommandRecipes] An error has occurred while reading the config file! See below for more info:");
        TShock.Log.Error(ex.ToString());
        return new RecConfig();
      }
    }

    public static RecConfig Write(RecConfig config) {
      File.WriteAllText(CommandRecipes.configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
      return config;
    }
  }
}
