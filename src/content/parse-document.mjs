import fs from "node:fs/promises";
import path from "node:path";
import matter from "gray-matter";
import { unified } from "unified";
import remarkParse from "remark-parse";
import remarkGfm from "remark-gfm";
import remarkRehype from "remark-rehype";
import rehypeSanitize from "rehype-sanitize";
import rehypeSlug from "rehype-slug";
import rehypeStringify from "rehype-stringify";
import { visit } from "unist-util-visit";

export async function parseDocument(projectRoot, relativePath) {
  const absolutePath = path.join(projectRoot, relativePath);
  const raw = await fs.readFile(absolutePath, "utf8");
  const parsed = matter(raw);
  const markdownAst = unified().use(remarkParse).use(remarkGfm).parse(parsed.content);
  const headings = [];
  const links = [];

  visit(markdownAst, (node) => {
    if (node.type === "heading") {
      const text = node.children
        .filter((child) => "value" in child)
        .map((child) => child.value)
        .join(" ")
        .trim();
      headings.push({
        depth: node.depth,
        text
      });
    }

    if (node.type === "link") {
      links.push(node.url);
    }
  });

  const html = String(
    await unified()
      .use(remarkParse)
      .use(remarkGfm)
      .use(remarkRehype)
      .use(rehypeSanitize)
      .use(rehypeSlug)
      .use(rehypeStringify)
      .process(parsed.content)
  );

  return {
    absolutePath,
    relativePath,
    frontmatter: parsed.data ?? {},
    excerpt: parsed.excerpt ?? "",
    body: parsed.content,
    raw,
    headings,
    links,
    html
  };
}

