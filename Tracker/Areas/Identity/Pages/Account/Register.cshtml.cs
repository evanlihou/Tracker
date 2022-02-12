// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Tracker.Models;

namespace Tracker.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
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
