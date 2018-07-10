; source: http://www.cplusplus.com/reference/cctype/
; needs testing

global isalpha, isdigit, isxdigit, isalnum
global islower, isupper
global tolower, toupper
global iscntrl, isspace, isblank
global isprint, isgraph, ispunct

segment .text

; int isalpha(int);
isalpha:
    or dil, 32   ; convert to lowercase
    cmp edi, 'a' ; only test lowercase range
    jl .ret_0
    cmp edi, 'z'
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0:
    xor eax, eax ; return zero
    ret
; int isdigit(int);
isdigit:
    cmp edi, '0'
    jl .ret_0
    cmp edi, '9'
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0: 
    xor eax, eax ; return zero
    ret
; int isxdigit(int);
isxdigit:
    cmp edi, '0'
    jl .not_digit
    cmp edi, '9'
    jg .not_digit
    
    mov eax, edi ; return nonzero
    ret
    
    .not_digit:
    or dil, 32   ; convert to lowercase
    cmp edi, 'a' ; only test lowercase range
    jl .ret_0
    cmp edi, 'f'
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0: 
    xor eax, eax ; return zero
    ret
; int isalnum(int);
isalnum:
    call isdigit ; do digit first because it doesn't modify edi
    mov esi, eax
    call isalpha ; or it with alpha
    or eax, esi
    ret

; int islower(int);
islower:
    cmp edi, 'a'
    jl .ret_0
    cmp edi, 'z'
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0:
    xor eax, eax ; return zero
    ret
; int isupper(int);
isupper:
    cmp edi, 'A'
    jl .ret_0
    cmp edi, 'Z'
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0:
    xor eax, eax ; return zero
    ret
    
; int tolower(int);
tolower:
    mov esi, edi ; store char in esi
    call isalpha
    cmp eax, 0
    jz .ret ; if not alpha, return esi
    
    or sil, 32 ; convert to lower
    
    .ret:
    mov eax, esi ; return esi
    ret
; int toupper(int);
toupper:
    mov esi, edi ; store char in esi
    call isalpha
    cmp eax, 0
    jz .ret ; if not alpha, return esi
    
    and sil, ~32 ; convert to upper
    
    .ret:
    mov eax, esi ; return esi
    ret

; int iscntrl(int);
iscntrl:
    cmp edi, 0x7f
    je .ret_true
    cmp edi, 0
    jl .ret_0
    cmp edi, 0x1f
    jg .ret_0
    
    .ret_true:
    mov eax, edi ; return nonzero
    inc eax      ; range includes null char
    ret
    
    .ret_0:
    xor eax, eax ; return zero
    ret
; int isspace(int);
isspace:
    cmp edi, 0x20
    je .ret_true
    cmp edi, 0x09
    jl .ret_0
    cmp edi, 0x0d
    jg .ret_0
    
    .ret_true:
    mov eax, edi ; return nonzero
    ret
    
    .ret_0:
    xor eax, eax ; return zero
    ret
; int isblank(int);
isblank:
    cmp edi, 0x09
    je .ret_true
    cmp edi, 0x20
    jne .ret_0
    
    .ret_true:
    mov eax, edi ; return nonzero
    ret
    
    .ret_0:
    xor eax, eax ; return zero
    ret
    
; int isprint(int);
isprint:
    cmp edi, 0x7f
    je .ret_0
    cmp edi, 0x20
    jl .ret_0
    cmp edi, 0xff
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0: 
    xor eax, eax ; return zero
    ret
; int isgraph(int);
isgraph:
    cmp edi, 0x7f
    je .ret_0
    cmp edi, 0x21
    jl .ret_0
    cmp edi, 0xff
    jg .ret_0
    
    mov eax, edi ; return nonzero
    ret
    
    .ret_0: 
    xor eax, eax ; return zero
    ret
; int ispunct(int);
ispunct:
    call isgraph ; must be a graph char
    cmp eax, 0
    jz .ret
    
    call isalnum ; must not be alnum
    cmp eax, 0
    jnz .ret_0
    
    inc eax ; return nonzero
    ret
    
    .ret_0: xor eax, eax ; return zero
    .ret: ret            ; return eax
