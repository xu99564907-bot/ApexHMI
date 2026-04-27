using System.Collections.Generic;
using System.Threading.Tasks;
using ApexHMI.Models;

namespace ApexHMI.Interfaces;

public interface IRecipeService
{
    Task SaveAsync(string path, IEnumerable<RecipeItem> items);
    Task<List<RecipeItem>> LoadAsync(string path);
}
