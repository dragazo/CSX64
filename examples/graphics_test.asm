global main

segment .text

main:
    call render
    
    ; pause for a bit (spin wait)
    mov ecx, 10000
    .wait: loop .wait
    
    jmp main

update_cursor_pos:
    mov eax, sys_getmousepos
    syscall
    
    ; center on x
    mov eax, [rect_w]
    shr eax, 1
    sub ebx, eax
    
    ; center on y
    mov eax, [rect_h]
    shr eax, 1
    sub ecx, eax
    
    mov [rect_x], ebx
    mov [rect_y], ecx
    
    ret
    
render:
    ; clear the screen
    mov eax, sys_clear
    mov ebx, [back_color]
    syscall
    
    ; update cursor pos
    call update_cursor_pos
    
    ; get proper brush for rendering cursor
    mov eax, sys_getmousedown
    syscall
    movz ebx, mouse_brush
    movnz ebx, mouse_down_brush
    mov eax, sys_setbrush
    syscall
    
    ; draw cursor
    mov eax, sys_fillellipse
    mov ebx, rect
    syscall
    
    ; render the frame
    mov eax, sys_render
    syscall
    
    ret

; -----------------------------------

segment .data

align 4
back_color: dd 0xff7da3e0

mouse_brush:
    mouse_brush_type: db 0
    mouse_brush_fore: dd 0xffd1942b
    mouse_brush_back: dd 0
mouse_down_brush:
    mouse_down_brush_type: db  0
    mouse_down_brush_fore: dd 0xffc94220
    mouse_down_brush_back: dd 0

align 4
rect:
    rect_x: dd 0
    rect_y: dd 0
    rect_w: dd 30
    rect_h: dd 30
    













