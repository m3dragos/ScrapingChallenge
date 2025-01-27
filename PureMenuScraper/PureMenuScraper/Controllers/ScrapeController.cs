﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PureMenuScraper.Scraping;
using System.Collections.Generic;

namespace PureMenuScraper.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ScrapeController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IMenuScraper _menuScrapper;

        public ScrapeController(ILogger<ScrapeController> logger, IMenuScraper menuScrapper)
        {
            _logger = logger;
            _menuScrapper = menuScrapper;
        }

        [HttpPost]
        public IEnumerable<MenuDishItem> Get(string menuUrl)
        {
            var items = _menuScrapper.GetAllDishes(menuUrl);

            return items;
        }
    }
}
