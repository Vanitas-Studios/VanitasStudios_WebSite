export const documentReducer = (state, action) => {
    switch (action.type) {
        case 'ADD_SECTION':
            return [...state, action.payload.section]

        case 'UPDATE_SECTION':
            return state.map(section => ({
                ...section,
                title: section.id === action.payload.id ? action.payload.title : section.title
            }));

        case 'DELETE_SECTION':
            return state.filter(section => section.id !== action.payload.id);

        case 'ADD_BLOCK_TO_SECTION':
            return state.map(section => {
                if (section.id === action.payload.id) { 
                    return {
                        ...section,
                        children: [...section.children, action.payload.block]
                    }
                }

                return section;
            })

        case 'UPDATE_BLOCK':
            return state.map(section => {
                if (section.id === action.payload.id) return action.payload.block

                return {
                    ...section,
                    children: section.children.map(block => 
                        block.id === action.payload.block.id? action.payload.block : block
                    )
                }
            })

        case 'DELETE_BLOCK':
            return state.map(section => ({
                ...section,
                children: section.children.filter(block => block.id !== action.payload.id)
            }));

        default:
            return state
    };
};