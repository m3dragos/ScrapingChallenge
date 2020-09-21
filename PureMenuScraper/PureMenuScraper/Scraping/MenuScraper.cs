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
            var popupCloseButton = RecoverableScrape(() => driver.FindElement(By.CssSelector(".popmake-close")), "MailingPopup");
            if (popupCloseButton != null)
            {
                popupCloseButton.Click();
            }
        }

        private IEnumerable<string> GetSubmenuUrls(IWebDriver driver)
        {
            var menusLink = Scrape(() => driver.FindElement(By.CssSelector("nav a[href='/menus/']")));
            var menuContainer = Scrape(() => menusLink.FindElement(By.XPath("./..")));
            var menus = Scrape(() => menuContainer.FindElements(By.CssSelector(".submenu > li > a")));

            return menus.Select(m => m.GetAttribute("href")).ToList();
        }

        private IEnumerable<MenuDishItem> ScrapeSubmenu(IWebDriver driver, string url)
        {
            var results = new List<MenuDishItem>();
            var itemLinkMapping = new List<KeyValuePair<string, MenuDishItem>>();
            var cultureInfo = new CultureInfo("en-GB", false);

            driver.Navigate().GoToUrl(url);

            // Scraping the menu title and description
            var menuContainer = Scrape(() => driver.FindElement(By.CssSelector("body > main > section")));
            var menuTitleElement = RecoverableScrape(() => menuContainer.FindElement(By.CssSelector(".menu-header > h1, h2")), nameof(MenuDishItem.MenuTitle));
            var menuDescriptionElement = RecoverableScrape(() => menuContainer.FindElement(By.CssSelector(".menu-header > p")), nameof(MenuDishItem.MenuDescription));
            
            var menuTitle = menuTitleElement?.Text ?? string.Empty; //Final value: MenuTitle
            var menuDescription = menuDescriptionElement?.Text ?? string.Empty; //Final value: MenuDescription

            // Multiple sections per menu
            var sections = Scrape(() => menuContainer.FindElements(By.CssSelector(".menu-title > a")));
            foreach (var sectionHeader in sections)
            {
                var titleElement = RecoverableScrape(() => sectionHeader.FindElement(By.CssSelector("span")), nameof(MenuDishItem.MenuSectionTitle));
                var titleText = titleElement?.Text ?? string.Empty; 
                var sectionTitle = cultureInfo.TextInfo.ToTitleCase(titleText.ToLower(cultureInfo)); //Final value: MenuSectionTitle

                var sectionId = new Uri(sectionHeader.GetAttribute("href")).Fragment;
                var sectionContainer = Scrape(() => menuContainer.FindElement(By.CssSelector(sectionId))); //'sectionId' already includes the '#' character
                var sectionItems = Scrape(() => sectionContainer.FindElements(By.CssSelector(".menu-item > a")));

                // Multiple items per section
                foreach (var sectionItem in sectionItems)
                {
                    var itemLink = RecoverableScrape(() => sectionItem.GetAttribute("href"), "DishUri");
                    var itemTitle = RecoverableScrape(() => sectionItem.GetAttribute("title"), nameof(MenuDishItem.DishName)) ?? string.Empty;

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
                    if (itemLink != null)
                    {
                        itemLinkMapping.Add(new KeyValuePair<string, MenuDishItem>(itemLink, resultItem));
                    }
                }
            }

            // Scrape item descriptions from their own pages
            foreach (var kvp in itemLinkMapping)
            {
                driver.Navigate().GoToUrl(kvp.Key);

                var descriptionElement = RecoverableScrape(() => driver.FindElement(By.CssSelector(".menu-item-details > div:nth-child(3) > :first-child")), nameof(MenuDishItem.DishDescription));
                kvp.Value.DishDescription = descriptionElement?.Text ?? string.Empty;

                _logger.LogInformation($"Scraping complete for dish \"{kvp.Value.DishName}\".");
            }

            return results;
        }

        private T Scrape<T>(Func<T> func) where T : class
        {
            try
            {
                return func.Invoke();
            }
            catch (NoSuchElementException e)
            {
                //TODO: include caller method/parameters in log
                _logger.LogWarning($"Error while scraping, aborting.");

                throw new Exception("Scraping exception", e);
            }
        }

        private T RecoverableScrape<T>(Func<T> func, string fieldName) where T : class
        {
            try
            {
                return func.Invoke();
            }
            catch (NoSuchElementException)
            {
                //TODO: include caller method/parameters in log
                _logger.LogWarning($"Error while reading {fieldName}, continuing and leaving it empty.");

                return null;
            }
        }
    }
}
