// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Tracker.Models;

namespace Tracker.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToActionPermanent("Index", "Home");
        }

        public IActionResult OnPost()
        {
            return RedirectToActionPermanent("Index", "Home");
        }
    }
}
