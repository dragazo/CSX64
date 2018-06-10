global strlen

segment .text

strlen: ; int strlen(const char *str)
    xor rax, rax ; zero rax (counter)
    jmp .aft
    .top: inc rax
    .aft:
        cmp [rdi + rax], 0
        jnz .top ; if this byte is zero, we're done
    ret ; return count