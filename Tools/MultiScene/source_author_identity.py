"""Canonical identity keys for legacy and URL-based source-author reservations."""
def author_identity(value):
    value = str(value or '').strip().lower().rstrip('/')
    for prefix in ['openimages-v7:', 'flickr-profile:']:
        if value.startswith(prefix):
            value = value[len(prefix):]
    for prefix in ['https://', 'http://']:
        if value.startswith(prefix):
            value = value[len(prefix):]
            break
    if value.startswith('www.'):
        value = value[4:]
    return value


def reservation_author_keys(row):
    """Keep stable groups when attribution is a human-readable credit string."""
    values = [row.get('group')]
    attribution = row.get('attribution')
    if isinstance(attribution, dict):
        values.append(attribution.get('AuthorProfileURL'))
    return {author_identity(value) for value in values if value}
