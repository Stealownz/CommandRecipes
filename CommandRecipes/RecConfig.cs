﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace CommandRecipes
{
	public class RecItem
	{
		public string name;
		public int stack;
		public int prefix;

		public RecItem(string name, int stack, int prefix = -1)
		{
			this.name = name;
			this.stack = stack;
			this.prefix = prefix;
		}

		public RecItem Clone()
		{
			return MemberwiseClone() as RecItem;
		}

		// Operators for explicit conversions
		public static explicit operator Terraria.Item(RecItem item)
		{
			var titem = new Terraria.Item();
			titem.SetDefaults(item.name);
			titem.stack = item.stack;
			titem.prefix = (byte)item.prefix;
			return titem;
		}

		public static explicit operator RecItem(Terraria.Item item)
		{
			return new RecItem(item.name, item.stack, item.prefix);
		}
	}

	public class Ingredient
	{
		public string name;
		public int stack;
		public int prefix;
		public int group;

		public Ingredient(string name, int stack, int prefix, int group)
		{
			this.name = name;
			this.stack = stack;
			this.prefix = prefix;
			this.group = group;
		}

		public Ingredient Clone()
		{
			return MemberwiseClone() as Ingredient;
		}

		public static explicit operator Terraria.Item(Ingredient item)
		{
			var titem = new Terraria.Item();
			titem.SetDefaults(item.name);
			titem.stack = item.stack;
			titem.prefix = (byte)item.prefix;
			return titem;
		}
	}

	public class Product
	{
		public string name;
		public int stack;
		public int prefix;
		public int group;
		public int weight;

		public Product(string name, int stack, int prefix, int group, int weight)
		{
			this.name = name;
			this.stack = stack;
			this.prefix = prefix;
			this.group = group;
			this.weight = weight;
		}

		public Product Clone()
		{
			return MemberwiseClone() as Product;
		}

		public static explicit operator Terraria.Item(Product item)
		{
			var titem = new Terraria.Item();
			titem.SetDefaults(item.name);
			titem.stack = item.stack;
			titem.prefix = (byte)item.prefix;
			return titem;
		}
	}

	public class LogRecipe
	{
		public string name;
		public List<RecItem> ingredients;
		public List<RecItem> products;

		public LogRecipe(string name, List<RecItem> ingredients, List<RecItem> products)
		{
			this.name = name;
			this.ingredients = ingredients;
			this.products = products;
		}
	}

	public abstract class RecipeFactory
	{
		public abstract Recipe Clone();
	}

	public class Recipe : RecipeFactory
	{
		public string name;
		public List<Ingredient> ingredients;
		public List<Product> products;
		public List<string> categories = new List<string>();
		public List<string> permissions = new List<string>();
		public List<string> regions = new List<string>();

		public Recipe(string name, List<Ingredient> ingredients, List<Product> products, List<string> categories = null, List<string> permissions = null, List<string> regions = null)
		{
			this.name = name;
			this.ingredients = ingredients;
			this.products = products;
			this.categories = categories;
			this.permissions = permissions;
			this.regions = regions;
		}

		public override Recipe Clone()
		{
			var clone = (Recipe)MemberwiseClone();
			clone.ingredients = new List<Ingredient>(ingredients.Count);
			clone.products = new List<Product>(products.Count);
			ingredients.ForEach(i => clone.ingredients.Add(i.Clone()));
			products.ForEach(i => clone.products.Add(i.Clone()));
			return clone;
		}
	}

	public class RecConfig
	{
		public bool CraftFromInventory = false;
		public List<Recipe> Recipes;

		public static RecConfig Read(string path)
		{
			if (!File.Exists(path))
				return new RecConfig();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return Read(fs);
			}
		}

		public static RecConfig Read(Stream stream)
		{
			using (var sr = new StreamReader(stream))
			{
				var cf = JsonConvert.DeserializeObject<RecConfig>(sr.ReadToEnd());
				if (ConfigRead != null)
					ConfigRead(cf);
				return cf;
			}
		}

		public void Write(string path)
		{
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				Write(fs);
			}
		}

		public void Write(Stream stream)
		{
			{
				Recipes = new List<Recipe>();
				Recipes.Add(new Recipe("Copper Broadsword",
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
				Recipes.Add(new Recipe("Iron Broadsword",
					new List<Ingredient>() { 
						new Ingredient("Iron Bar", 8, 0, 0), 
						new Ingredient("Stone Block", 20, 0, 0), 
						new Ingredient("Wooden Hammer", 1, -1, 0) },
					new List<Product>() { 
						new Product("Iron Broadsword", 1, 41, 0, 100), 
						new Product("Wooden Hammer", 1, 39, 0, 100) },
					new List<string> { "Example", "Example2" }, 
					new List<string> { "cmdrec.craft.example", "craft" }, 
					new List<string> { "" }));
			}

			var str = JsonConvert.SerializeObject(this, Formatting.Indented);
			using (var sw = new StreamWriter(stream))
			{
				sw.Write(str);
			}
		}

		public static Action<RecConfig> ConfigRead;
	}
}
