export interface SpecLinkCitationTarget {
  excerpt?: string | null;
  fileName?: string | null;
  documentTitle?: string | null;
}

/**
 * Extracts the document name prefix before section markers such as " - ", " | ", or "#".
 */
export function extractCiteDocName(cite: string): string {
  return (cite || '').split(/\s+-\s+|\s+\|\s+|#/)[0]?.trim() || cite;
}

/**
 * Filters out raw document citations that are already covered by linked Spec Hub entries.
 */
export function filterUnlinkedCitations(
  citations: readonly string[] | undefined | null,
  links: readonly SpecLinkCitationTarget[] | undefined | null
): string[] {
  const list = citations ?? [];
  const existingLinks = links ?? [];
  if (existingLinks.length === 0) return [...list];

  return list.filter(cite => {
    const lowerCite = cite.toLowerCase();
    return !existingLinks.some(link =>
      (link.excerpt && link.excerpt.toLowerCase().includes(lowerCite)) ||
      (link.fileName && lowerCite.includes(link.fileName.toLowerCase())) ||
      (link.documentTitle && lowerCite.includes(link.documentTitle.toLowerCase()))
    );
  });
}
