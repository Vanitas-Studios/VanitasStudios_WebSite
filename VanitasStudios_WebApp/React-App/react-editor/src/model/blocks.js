export const BlockTypes = {
    SECTION: 'section', 
    PARAGRAPH: 'paragraph',
    CODE: 'code',
    QUOTE: 'quote',
    MEDIA: 'media'
}

export const createBlock = (type, data = {}) => {
    return {
        id: crypto.randomUUID(),
        type,
        ...data,
        children: type === BlockTypes.SECTION ? [] : undefined
    };
};