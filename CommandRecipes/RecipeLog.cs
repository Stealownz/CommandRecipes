using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CommandRecipes {
  public class RecipeLog {
    public RecipeLog() {

    }

    private static string path = Path.Combine(CommandRecipes.configDir, "CraftLog.txt");
    public StreamWriter Writer { get; protected set; }
    
    public void Initialize() {
      Writer = new StreamWriter(path, true);
    }
    public void Dispose() {
      Writer.Close();
    }
    
    /// <summary>
    /// Logs a crafted recipe to the log file.
    /// </summary>
    public void Recipe(string recName, List<RecItem> recIngredients, List<RecItem> recProducts, string player) {
      try {
        var list = new List<string>();
        recIngredients.ForEach(i => list.Add(Utils.FormatItem((Item)i, i.stack)));
        string ingredients = String.Join(",", list);
        list.Clear();
        recProducts.ForEach(i => list.Add(Utils.FormatItem((Item)i, i.stack)));
        string products = String.Join(",", list);
        var str = String.Format("{0}: Player ({1}) crafted recipe ({2}), using ({3}) and obtaining ({4}).",
          DateTime.UtcNow,
          player,
          recName,
          ingredients,
          products);
        Writer.WriteLine(str);
        Writer.Flush();
      }
      catch (Exception ex) {
        TShock.Log.ConsoleError(ex.ToString());
      }
    }
  }
}
