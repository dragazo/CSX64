global strlen
global strcpy
global strcmp

# ($1:64 char*) -> ($0:64 length)
# finds the length of a null-terminated string
strlen:
    xor $0, $0
    .top:
        cmpz:16 [$1 + $0]
        je [.ret]
        add $0, 2
        jmp [.top]
    .ret: ret

# ($1:64 char *dest) ($2:64 char *source) -> ($0:64 char *dest)
# finds the length of a null-terminated string
strcpy:
    xor $0, $0
    .top:
        # copy the character
        mov:16 $3, [$2 + $0]
        mov:16 [$1 + $0], $3
        
        # if it was zero, we're done
        cmpz:16 $3
        je [.ret]
        add $0, 2
        jmp [.top]
    .ret:
        mov $0, $1
        ret

# ($1:64 char *a) ($2:64 char *b) -> ($0:64 <0, 0, or >0)
# finds the length of a null-terminated string
strcmp:
    xor $3, $3
    .top:
        # compare a character
        mov:16 $4, [$1 + $3]
        mov:16 $3, $4
        sub:16 $3, [$2 + $3]
        
        # if it was different, we have our result
        jnz [.ret]
        # otherwise, if we're at a terminator, we have our answer
        cmpz:16 $4
        je [.ret]
        
        # on to the next character
        add $3, 2
        jmp [.top]
    .ret:
        # extend result to full 64 bits
        mov:16 $0, $3
        sx 16, $0
        ret




























