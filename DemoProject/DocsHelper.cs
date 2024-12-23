﻿using Markdig;
using System.Text;
using Stratus;

namespace CustomWebServer;
public class Page(string title, string content, Page? parent, bool isParent = false) {
	public string Title { get; set; } = title.Trim(' ');
	public string UrlName { get; set; } = ToUrlFriendly(title);
	public string Content { get; set; } = content;
	public bool IsParent { get; set; } = isParent;
	public Page? Parent { get; set; } = parent;
	public List<Page> Children { get; set; } = [];
	public bool HasChildren {
		get {
			return Children.Count != 0;
		}
	}

	public string Path {
		get {
			if (path == null) {
				var pathSegments = new List<string> { ToUrlFriendly(Title) };

				var currentPage = Parent;
				while (currentPage != null) {
					pathSegments.Insert(0, ToUrlFriendly(currentPage.Title));
					currentPage = currentPage.Parent;
				}

				path = string.Join("/", pathSegments).Replace(" ", "");
			}
			return path;
		}
	}
	private string? path;

	public List<Page> GetBreadcrumbs() {
		List<Page> breadcrumbs = [];
		var currentPage = this;
		while (currentPage != null) {
			breadcrumbs.Insert(0, currentPage);
			currentPage = currentPage.Parent;
		}
		return breadcrumbs;
	}

	private static string ToUrlFriendly(string title) {
		return title.ToLower().Replace(" ", "-");
	}
}

public class DocsHelper {
	public Page RootPage { get; set; }

	public DocsHelper(string rootFolder) {
		MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

		string[] subDirectories = Directory.GetDirectories(rootFolder);
		string[] files = Directory.GetFiles(rootFolder);

		string folderName = Path.GetFileName(rootFolder);
		var mdFilePath = Path.Combine(rootFolder, folderName + ".md");
		RootPage = new(CamelToSentence(folderName), "", null, true);

		foreach (string file in files) {
			string pageName = Path.GetFileNameWithoutExtension(file);

			if (pageName == folderName) {
				RootPage.Content = Markdown.ToHtml(File.ReadAllText(mdFilePath), pipeline);
				continue;
			}

			string content = Markdown.ToHtml(File.ReadAllText(file), pipeline);
			Page page = new(CamelToSentence(pageName), content, RootPage);
			RootPage.Children.Add(page);
		}

		foreach (var subDirectory in subDirectories) {
			BuildTree(subDirectory, RootPage);
		}
	}

	public void RouteDocs(Server server) {
		Route(RootPage, server);
	}

	public (List<string>, List<string>) GetStaticRoutes() {
		List<string> titles = [];
		List<string> paths = [];

		FillRoutes(RootPage, titles, paths);
		return (titles, paths);
	}

	private void FillRoutes(Page page, List<string> titles, List<string> paths) {
		titles.Add(page.Title);
		paths.Add(page.Path);

		foreach(Page subPage in page.Children) {
			FillRoutes(subPage, titles, paths);
		}
	}

	private void Route(Page page, Server server) {
		Console.WriteLine($"Bound: {page.Path}");

		List<Page> breadcrumbs = page.GetBreadcrumbs();

		server.Router.Get(page.Path, (context, parameters) => {
			return server.Renderer.RenderPage("Docs", new {
				page,
				breadcrumbs,
			}, 200, $"{server.Name} | {page.Title}");
		});
		foreach (Page child in page.Children) {
			Route(child, server);
		}
	}


	private void BuildTree(string folder, Page parentPage) {

		MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

		string[] subDirectories = Directory.GetDirectories(folder);
		string[] files = Directory.GetFiles(folder);

		string folderName = Path.GetFileName(folder);
		var mdFilePath = Path.Combine(folder, folderName + ".md");

		Page currentPage = new(CamelToSentence(folderName), "", parentPage, true);
		parentPage.Children.Add(currentPage);
		if (File.Exists(mdFilePath)) {
			currentPage.Content = Markdown.ToHtml(File.ReadAllText(mdFilePath), pipeline);
		}

		foreach (string file in files) {
			string pageName = Path.GetFileNameWithoutExtension(file);

			if (pageName == folderName) {
				continue;
			}

			string pageContent = Markdown.ToHtml(File.ReadAllText(file), pipeline);
			Page page = new(CamelToSentence(pageName), pageContent, currentPage);
			currentPage.Children.Add(page);
		}

		foreach (var subDirectory in subDirectories) {
			BuildTree(subDirectory, currentPage);
		}
	}

	private string CamelToSentence(string input) {
		if (string.IsNullOrEmpty(input))
			return input;

		StringBuilder result = new();

		result.Append(char.ToUpper(input[0]));

		for (int i = 1; i < input.Length; i++) {
			char currentChar = input[i];

			if (char.IsUpper(currentChar) && char.IsLower(input[i - 1])) {
				result.Append(' ');
			}

			result.Append(char.ToLower(currentChar));
		}

		return result.ToString();
	}
}

