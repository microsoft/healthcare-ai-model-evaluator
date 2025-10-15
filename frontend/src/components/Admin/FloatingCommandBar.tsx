import React, { useEffect, useState, useRef, useCallback } from 'react';
import { 
    CommandBar,
    ICommandBarItemProps,
    IStyleFunctionOrObject,
    ICommandBarStyleProps,
    ICommandBarStyles
} from '@fluentui/react';
interface FloatingCommandBarProps {
    items: ICommandBarItemProps[];
    farItems?: ICommandBarItemProps[];
    styles?: IStyleFunctionOrObject<ICommandBarStyleProps, ICommandBarStyles>;
    parentId?: string; // ID of the parent container to observe for scroll
    stickOffsetId?: string; // ID of the element to stick the command bar to
}

export const FloatingCommandBar: React.FC<FloatingCommandBarProps> = ({ items, farItems, styles, parentId, stickOffsetId }) => {
    const [isSticky, setIsSticky] = useState(false);
    const commandBarRef = useRef<HTMLDivElement>(null);
    const [left, setLeft] = useState<number | 'auto'>('auto');
    const [right, setRight] = useState<number | 'auto'>('auto');
    const [stickOffset, setStickOffset] = useState<number | 0>(0);
    const [top, setTop] = useState<number | 0>(0);

    const handleScroll = useCallback(() => {
        if (commandBarRef.current) {
            const rect = commandBarRef.current.getBoundingClientRect();
            const scrollThreshold = commandBarRef.current.offsetTop - stickOffset;
            setTop(stickOffset);
            setLeft(rect.left);
            setRight(rect.right);
            if (rect.top <= scrollThreshold - commandBarRef.current.offsetHeight && !isSticky) {
                console.log("Making command bar sticky", stickOffset);
                setIsSticky(true);
            } else if (rect.top > scrollThreshold - commandBarRef.current.offsetHeight && isSticky) {
                setIsSticky(false);
            }
        }
    }, [isSticky]);

    useEffect(() => {
        if (parentId && stickOffsetId) {
            const stickOffsetElement = document.getElementById(stickOffsetId);
            if (stickOffsetElement) {
                setStickOffset(stickOffsetElement.offsetHeight);
            }
            const parentElement = document.getElementById(parentId);
            if (parentElement) {
                parentElement.addEventListener('scroll', handleScroll);
                console.log("Added scroll listener");
                return () => parentElement.removeEventListener('scroll', handleScroll);
            }
        }
        window.addEventListener('scroll', handleScroll);
        return () => window.removeEventListener('scroll', handleScroll);
    }, [handleScroll, parentId]);

    const stickyStyles: IStyleFunctionOrObject<ICommandBarStyleProps, ICommandBarStyles> = {
        root: {
            display: isSticky ? 'flex' : 'none',
            position: isSticky ? 'fixed' : 'relative',
            top: isSticky ? top : 'auto',
            left: isSticky ? left : 'auto',
            right: isSticky ? right : 'auto',
            zIndex: isSticky ? 10 : 'auto',
            width: isSticky ? '100%' : 'auto',
            borderBottom: isSticky ? '1px solid #ccc' : 'none',
            ...((typeof styles === 'function' 
                ? (styles({} as ICommandBarStyleProps).root || {})
                : (styles?.root || {})) as object)
        }
    };

    return (
        <div ref={commandBarRef} style={{ position: 'relative' }}>
            <CommandBar
                items={items}
                farItems={farItems}
                styles={{
                    root: {
                        borderBottom: '1px solid #eee',
                    }
                }}
            />
            <CommandBar
                items={items}
                farItems={farItems}
                styles={stickyStyles}
            />
        </div>
    );
}
