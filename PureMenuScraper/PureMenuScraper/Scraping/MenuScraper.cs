using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PureMenuScraper.Scraping
{
    public class MenuScraper : IMenuScraper
    {
        private ILogger _logger;

        public MenuScraper(ILogger<MenuScraper> logger)
        {
            _logger = logger;
        }

        public IEnumerable<MenuDishItem> GetAllDishes(string url)
        {
            var results = new List<MenuDishItem>();

            using (var driver = new ChromeDriver())
            {
                driver.Navigate().GoToUrl(url);

                CheckAndDismissPopup(driver);

                var submenuUrls = GetSubmenuUrls(driver);
                foreach (var submenuUrl in submenuUrls)
                {
                    var submenuItems = ScrapeSubmenu(driver, submenuUrl);
                    results.AddRange(submenuItems);
                }
            }

            return results;
        }

        private void CheckAndDismissPopup(IWebDriver driver)
        {
            try
            {
                var popupCloseButton = driver.FindElement(By.CssSelector(".popmake-close"));
                popupCloseButton.Click();
            }
            catch (NoSuchElementException)
            {
                //popup not found, no need to do anything
                return;
            }
        }

        private IEnumerable<string> GetSubmenuUrls(IWebDriver driver)
        {
            var menusLink = driver.FindElement(By.CssSelector("nav a[href='/menus/']"));
            var menuContainer = menusLink.FindElement(By.XPath("./.."));
            var menus = menuContainer.FindElements(By.CssSelector(".submenu > li > a"));

            return menus.Select(m => m.GetAttribute("href")).ToList();
        }

        private IEnumerable<MenuDishItem> ScrapeSubmenu(IWebDriver driver, string url)
        {
            var results = new List<MenuDishItem>();
            var itemLinkMapping = new List<KeyValuePair<string, MenuDishItem>>();

            driver.Navigate().GoToUrl(url);

            var menuContainer = driver.FindElement(By.CssSelector("body > main > section"));
            var menuTitle = menuContainer.FindElement(By.CssSelector(".menu-header > h1, h2")).Text;
            var menuDescription = menuContainer.FindElement(By.CssSelector(".menu-header > p")).Text;
            var sections = menuContainer.FindElements(By.CssSelector(".menu-title > a"));

            foreach (var sectionHeader in sections)
            {
                var cultureInfo = new CultureInfo("en-GB", false);
                var titleText = sectionHeader.FindElement(By.CssSelector("span")).Text;
                var sectionTitle = cultureInfo.TextInfo.ToTitleCase(titleText.ToLower(cultureInfo));

                var sectionId = new Uri(sectionHeader.GetAttribute("href")).Fragment;
                var sectionContainer = menuContainer.FindElement(By.CssSelector(sectionId)); //'sectionId' already includes the '#' character
                var sectionItems = sectionContainer.FindElements(By.CssSelector(".menu-item > a"));

                foreach (var sectionItem in sectionItems)
                {
                    var itemLink = sectionItem.GetAttribute("href");
                    var itemTitle = sectionItem.GetAttribute("title");
                    var resultItem = new MenuDishItem
                    {
                        MenuTitle = menuTitle,
                        MenuDescription = menuDescription,
                        MenuSectionTitle = sectionTitle,
                        DishName = itemTitle
                    };

                    results.Add(resultItem);

                    // Item description is on the product page. Save link for later, we will scrape all of them at once
                    //to avoid refreshing this page, which we are not done with yet.
                    itemLinkMapping.Add(new KeyValuePair<string, MenuDishItem>(itemLink, resultItem));
                }
            }

            foreach (var kvp in itemLinkMapping)
            {
                driver.Navigate().GoToUrl(kvp.Key);

                var description = driver.FindElement(By.CssSelector(".menu-item-details > div:nth-child(3) > :first-child")).Text;
                kvp.Value.DishDescription = description;
            }

            return results;
        }
    }
}
