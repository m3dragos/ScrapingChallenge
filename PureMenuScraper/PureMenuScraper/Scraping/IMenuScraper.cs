using System.Collections.Generic;

namespace PureMenuScraper.Scraping
{
    public interface IMenuScraper
    {
        IEnumerable<MenuDishItem> GetAllDishes(string url);
    }
}
