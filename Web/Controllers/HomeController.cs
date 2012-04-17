﻿using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using System.Web.Routing;
using Compilify.Models;
using Compilify.Services;
using Compilify.Web.Models;
using Compilify.Web.Services;

namespace Compilify.Web.Controllers
{
    public class HomeController : AsyncController
    {
        public HomeController(IPostRepository contentRepository)
        {
            db = contentRepository;
            compiler = new CSharpCompiler();
        }

        private readonly IPostRepository db;
        private readonly CSharpCompiler compiler;

        [HttpGet]
        public ActionResult Index()
        {
            var post = (TempData["Post"] as Post) ?? new Post();
            if (string.IsNullOrEmpty(post.Classes))
            {
                var classesBuilder = new StringBuilder();

                classesBuilder.AppendLine("public interface IPerson");
                classesBuilder.AppendLine("{");
                classesBuilder.AppendLine("    string Name { get; }");
                classesBuilder.AppendLine();
                classesBuilder.AppendLine("    string Greet();");
                classesBuilder.AppendLine("}");
                classesBuilder.AppendLine();
                classesBuilder.AppendLine("class Person : IPerson");
                classesBuilder.AppendLine("{");
                classesBuilder.AppendLine("    public Person(string name)");
                classesBuilder.AppendLine("    {");
                classesBuilder.AppendLine("        Name = name;");
                classesBuilder.AppendLine("    }");
                classesBuilder.AppendLine();
                classesBuilder.AppendLine("    public string Name { get; private set; }");
                classesBuilder.AppendLine();
                classesBuilder.AppendLine("    public string Greet()");
                classesBuilder.AppendLine("    {");
                classesBuilder.AppendLine("        if (Name == null)");
                classesBuilder.AppendLine("            return \"Hello, stranger!\";");
                classesBuilder.AppendLine();
                classesBuilder.AppendLine("        return string.Format(\"Hello, {0}!\", Name);");
                classesBuilder.AppendLine("    }");
                classesBuilder.AppendLine("}");

                post.Classes = classesBuilder.ToString();

                var commandBuilder = new StringBuilder();

                commandBuilder.AppendLine("IPerson person = new Person(name: null);");
                commandBuilder.AppendLine("");
                commandBuilder.AppendLine("return person.Greet();");

                post.Content = commandBuilder.ToString();
            }
            
            var viewModel = new PostViewModel
                            {
                                Post = post,
                                Errors = compiler.GetCompilationErrors(post.Content, post.Classes)
                            };

            return View("Show", viewModel);
        }

        [HttpGet]
        public ActionResult About()
        {
            return View();
        }

        // GET /:slug           -> Equivilant to /:slug/1
        // GET /:slug/:version  -> Get a specific version of the content
        // GET /:slug/latest    -> Get the latest saved version of the content
        // GET /:slug/live      -> Watch or collaborate on the content in real time
        //
        [HttpGet]
        public ActionResult Show(string slug, int? version)
        {
            if (version <= 1)
            {
                // Redirect the user to /:slug instead of /:slug/1
                return RedirectToActionPermanent("Show", "Home", new { slug = slug, version = (int?)null });
            }

            version = version ?? 1;
            var post = db.GetVersion(slug, version.Value);

            if (post == null)
            {
                Response.StatusCode = 404;
                ViewBag.Message = string.Format("code snippet of '{0}' ver. {1} was not found.", slug, version.Value);
                return View("Error");
            }

            var viewModel = new PostViewModel
                            {
                                Post = post, 
                                Errors = compiler.GetCompilationErrors(post.Content, post.Classes)
                            };

            if (Request.IsAjaxRequest())
            {
                return Json(new { status = "ok", data = viewModel }, JsonRequestBehavior.AllowGet);
            }
            
            return View("Show", viewModel);
        }
        
        [HttpGet]
        public ActionResult Latest(string slug)
        {
            var latest = db.GetLatestVersion(slug);

            if (latest < 1)
            {
                return HttpNotFound();
            }

            return RedirectToAction("Show", "Home", new { slug = slug, version = latest });
        }

        [HttpPost]
        public ActionResult Save(string slug, Post post)
        {
            var result = db.Save(slug, post);

            var routeValues = new RouteValueDictionary { { "slug", result.Slug } };

            if (result.Version > 1)
            {
                routeValues.Add("version", result.Version);
            }

            return RedirectToAction("Show", routeValues);
        }

        [HttpPost]
        public ActionResult Import(Uri address)
        {
            if (address == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var code = string.Empty;

            if (address.Host.Contains("pastebin.com"))
            {
                var pasteId = address.AbsolutePath.Replace("/", "");
                using (var client = new WebClient())
                {
                    code = client.DownloadString(string.Format("http://pastebin.com/raw.php?i={0}", pasteId));
                }
            }

            TempData["Post"] = new Post { Classes = code };
            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Validate(ValidateViewModel viewModel)
        {
            var errors = compiler.GetCompilationErrors(viewModel.Command, viewModel.Classes)
                                 .ToArray();

            return Json(new { status = "ok", data = errors });
        }
    }
}
