# frozen_string_literal: true

# jekyll-seo-tag's JSON-LD publisher is an Organization with only a logo (it takes a name only from `author`, and
# never a url), which schema validators reject. Give it the Automate The Planet organization's @id, name and url,
# the same node _includes/jsonld.html emits, and the ATP logo instead of the SPECTRA one. <head> only.
require 'jekyll-seo-tag'

module SpectraSeoPublisher
  ORGANIZATION = {
    '@id' => 'https://www.automatetheplanet.com/#organization',
    'name' => 'Automate The Planet Ltd',
    'url' => 'https://www.automatetheplanet.com/'
  }.freeze

  def publisher
    output = super
    return output unless output

    logo = output['logo']
    if logo.is_a?(Hash) && logo['url']
      logo = logo.merge('url' => logo['url'].sub(%r{/assets/images/[^/]+\z}, '/assets/images/atp-logo.svg'))
    end
    output.merge(ORGANIZATION).merge('logo' => logo)
  end
end

Jekyll::SeoTag::JSONLDDrop.prepend(SpectraSeoPublisher)
