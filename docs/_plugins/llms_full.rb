# frozen_string_literal: true

# Writes /llms-full.txt: every public documentation page as plain Markdown in one file, for AI assistants
# (https://llmstxt.org). /llms.txt is a hand-written static file next to it.
#
# Built from each page's source Markdown before Liquid runs, so the output is Markdown, not HTML:
#   - {% include screenshot.html %} -> a Markdown image with its alt text (and caption)
#   - {% include flow.html %}       -> the flow's caption and steps from _data/flows.yml as a numbered list
#   - {% include spectra_card.html %} -> a link list item with the target page's title and description
#   - other Liquid tags are dropped; text inside {% raw %} stays as written
#   - relative links to other pages become absolute production URLs
# Pages with nav_exclude (moved-content stubs) are left out. Runs on the GitHub Actions build (docs.yml), where
# custom plugins are allowed.
module SpectraLlmsFull
  class Generator < Jekyll::Generator
    safe true
    priority :lowest

    def generate(site)
      @site = site
      @base = "#{site.config['url']}#{site.config['baseurl']}"
      @by_path = {}
      site.pages.each { |p| @by_path[p.path.tr('\\', '/')] = p }

      pages = ordered_pages
      out = +"# #{site.config['title']} documentation (full text)\n\n"
      out << "> #{site.config['description']}. Every page of #{@base}/ as Markdown, in navigation order. " \
             "Index of pages: #{@base}/llms.txt\n"
      pages.each { |p| out << page_block(p) }

      file = Jekyll::PageWithoutAFile.new(site, site.source, '', 'llms-full.txt')
      file.content = out
      file.data['layout'] = nil
      file.data['sitemap'] = false
      file.data['render_with_liquid'] = false # the Markdown above is final text, not a template
      site.pages << file
    end

    private

    def doc_page?(page)
      page.ext == '.md' && page.data['title'] && !page.data['nav_exclude'] && page.data['sitemap'] != false && page.html?
    end

    def order_key(page)
      [page.data['nav_order'] || 999, page.data['title'].to_s.downcase]
    end

    # Navigation order: top-level pages, each followed by its children and grandchildren (just-the-docs rules).
    def ordered_pages
      docs = @site.pages.select { |p| doc_page?(p) }
      top = docs.reject { |p| p.data['parent'] }.sort_by { |p| order_key(p) }
      result = []
      top.each do |t|
        result << t
        children = docs.select { |p| p.data['parent'] == t.data['title'] && !p.data['grand_parent'] }
        children.sort_by { |p| order_key(p) }.each do |c|
          result << c
          grand = docs.select { |p| p.data['parent'] == c.data['title'] && p.data['grand_parent'] == t.data['title'] }
          result.concat(grand.sort_by { |p| order_key(p) })
        end
      end
      result.concat((docs - result).sort_by { |p| p.url })
    end

    def absolute(url)
      "#{@base}#{url}"
    end

    def page_block(page)
      title = (page.data['hero_title'] || page.data['title']).to_s
      body = convert(page.content.to_s, page)
      body = body.sub(/\A\s*#\s+#{Regexp.escape(title)}\s*\n/, '').strip
      block = +"\n\n---\n\n# #{title}\n\nURL: #{absolute(page.url)}\n"
      block << "Description: #{page.data['description']}\n" if page.data['description']
      block << "\n#{body}\n"
    end

    def convert(text, page)
      # Keep {% raw %} sections verbatim; convert Liquid only outside them.
      parts = text.split(/(\{%-?\s*raw\s*-?%\}.*?\{%-?\s*endraw\s*-?%\})/m)
      parts.map do |part|
        if part =~ /\A\{%-?\s*raw\s*-?%\}(.*)\{%-?\s*endraw\s*-?%\}\z/m
          Regexp.last_match(1)
        else
          markup(links(liquid(part), page))
        end
      end.join
    end

    # Theme markup that is noise in plain Markdown: kramdown attribute lists ({: .btn }), layout <div> lines,
    # and <details>/<summary> wrappers (the summary becomes a bold line).
    def markup(text)
      text.gsub(/\{:[^}\n]*\}/, '')
          .gsub(%r{^[ \t]*</?div\b[^>\n]*>[ \t]*\n}, '')
          .gsub(%r{^[ \t]*</?details\b[^>\n]*>[ \t]*\n}, '')
          .gsub(%r{^[ \t]*<summary>(.*?)</summary>[ \t]*$}, '**\1**')
    end

    def params(str)
      h = {}
      str.scan(/(\w+)=(?:"([^"]*)"|'([^']*)'|(\S+))/) { |k, a, b, c| h[k] = a || b || c }
      h
    end

    def liquid(text)
      text.gsub(/\{%-?\s*include\s+(\S+)(.*?)-?%\}/m) do
        name = Regexp.last_match(1)
        args = params(Regexp.last_match(2))
        case name
        when 'screenshot.html' then screenshot(args)
        when 'flow.html' then flow(args)
        when 'spectra_card.html' then card(args)
        else ''
        end
      end.gsub(/\{%.*?%\}/m, '')
    end

    def screenshot(args)
      img = "![#{args['alt']}](#{absolute("/assets/images/screenshots/#{args['file']}.webp")})"
      args['caption'] ? "#{img}\n\n*#{args['caption']}*" : img
    end

    def flow(args)
      flow = @site.data.dig('flows', args['name'])
      return '' unless flow

      lines = ["**#{flow['caption']}**", '']
      flow['steps'].each_with_index do |s, i|
        title = s['href'] ? "[#{s['title']}](#{absolute(s['href'])})" : s['title']
        lines << "#{i + 1}. **#{title}**: #{s['text']}"
      end
      lines.join("\n")
    end

    def card(args)
      target = @by_path[args['page']]
      return '' unless target

      desc = target.data['description'] || args['desc']
      "- [#{target.data['title']}](#{absolute(target.url)})#{desc ? ": #{desc}" : ''}"
    end

    # [text](relative/page.md#anchor) -> [text](https://host/base/page.html#anchor) when it names a page.
    def links(text, page)
      dir = File.dirname(page.path.tr('\\', '/'))
      # jekyll-relative-links (an earlier generator) has already turned .md links into /Spectra/... paths.
      site_path = %r{\]\((#{Regexp.escape(@site.config['baseurl'].to_s)}/[^)\s]*)\)}
      text = text.gsub(site_path) { "](#{@site.config['url']}#{Regexp.last_match(1)})" }
      text.gsub(/\]\((?!https?:|mailto:|#|\/)([^)\s#]+)(#[^)\s]*)?\)/) do
        target, anchor = Regexp.last_match(1), Regexp.last_match(2)
        rel = File.expand_path(target, "/#{dir}").sub(%r{\A[A-Za-z]:}, '').sub(%r{\A/}, '')
        hit = @by_path[rel] || @by_path["#{rel}.md"] || @by_path[rel.sub(/\.html\z/, '.md')]
        hit ? "](#{absolute(hit.url)}#{anchor})" : Regexp.last_match(0)
      end
    end
  end
end
