; source = http://www.cplusplus.com/reference/cstring/
; todo: strcoll
;       strxfrm
;       strcspn
;       strpbrk
;       strrchr
;       strspn
;       strstr
;       strtok
;       
;       + testing
;       

global memcpy
global memmove
global strcpy
global strncpy

global strcat
global strncat

global memcmp
global strcmp
global strncmp

global memchr
global strchr

global memset
global strerror
global strlen

; ----------------------

extern malloc
extern free

; ----------------------

segment .text

; void *memcpy(void *dest, const void *src, size_t num);
memcpy:
    mov rax, rdi ; copy dest into rax (for return value)

    ; for(int i = 0; i < num; ++i)
    xor rcx, rcx
    jmp .aft
    .top:
        ; copy the byte
        mov bl, [rsi+rcx]
        mov [rdi+rcx], bl
        
        inc rcx
    .aft:
        cmp rcx, rdx
        jb .top
    
    ret

; void *memmove(void *dest, const void *src, sizt_t num);
memmove:
    ; push args on stack for safety
    push rdi
    push rsi
    push rdx
    
    ; get a temp array
    mov rdi, rdx
    call malloc
    
    ; push temp array for safety
    push rax
    
    ; copy src to temp
    mov rdi, rax
    mov rsi, [rsp+16]
    mov rdx, [rsp+8]
    call memcpy
    
    ; copy temp to dest
    mov rdi, [rsp+24]
    mov rsi, [rsp]
    mov rdx, [rsp+8]
    call memcpy
    
    ; deallocate temp array
    pop rdi
    call free
    
    ; clean up stack
    pop rax
    pop rax
    pop rax
    
    ; return (rax now contains dest)
    ret

; char *strcpy(char *dest, const char *src);
strcpy:
    mov rax, rdi ; store dest in rax (for return value)
    
    .top:
        ; copy a char
        mov bl, [rsi]
        mov [rdi], bl
        
        ; increment dest/src pointers
        inc rdi
        inc rsi
        
        ; if char was non-zero, repeat
        cmp bl, 0
        jne .top
    
    ; return dest
    ret

; char *strncpy(char *dest, const char *src, size_t num);
strncpy:
    mov rax, rdi ; store dest in rax (for return value)
    
    jmp .aft
    .top:
        ; copy a char
        mov bl, [rsi]
        mov [rdi], bl
        
        ; increment dest/src pointers
        inc rdi
        inc rsi
        
        ; if char was null terminator, return
        cmp bl, 0
        je .ret
        
        ; decrement num
        dec rdx
    .aft:
        cmp rdx, 0 ; if num is above zero, enter loop
        ja .top
    
    ; getting here means num terminated us early - append null terminator
    mov byte ptr [rdi], 0
    
    ; return dest
    .ret: ret

; char *strcat(char *dest, const char *src);
strcat:
    ; push args for safekeeping
    push rsi
    push rdi
    
    ; get length of dest
    call strlen
    
    ; use strcpy to perform the copy
    pop rdi
    add rdi, rax ; copy dest is dest + len(dest)
    pop rsi
    call strcpy
    
    ; return dest
    ret
    
; char *strncat(char *dest, const char *src, size_t num);
strncat:
    ; push args for safekeeping
    push rdi ; extra copy of dest here for return value
    push rdx
    push rsi
    push rdi
    
    ; get length of dest
    call strlen
    
    ; use strncpy to perform the copy
    pop rdi
    add rdi, rax ; copy dest is dest + len(dest)
    pop rsi
    pop rdx ; recover num parameter
    call strncpy
    
    ; return dest
    pop rax
    ret
    
; int memcmp(const void *ptr1, const void *ptr2, size_t num); // ret >0 -> ptr1 > ptr2
memcmp:
    ; for(int count = 0; count < num; ++count)
    xor rcx, rcx ; zero count
    jmp .aft
    .top:
        ; get ptr1 byte
        mov al, [rdi+rcx]
        ; sub ptr2 byte
        sub al, [rsi+rcx]
        
        ; if that's non-zero, return it
        jnz .ret_diff
        
        inc rcx ; inc count
    .aft:
        cmp rcx, rdx
        jb .top
    
    .ret_same:
        ; zero return val
        xor eax, eax
        ret
    
    .ret_diff:
        ; sign extend difference to 32-bit
        cbw
        cwde
        ret

; int strcmp(const char *str1, const char *str2); // ret >0 -> str1 > str2
strcmp:
    ; for(int count = 0; count < num; ++count)
    xor rcx, rcx ; zero count
    .top:
        ; get str1 byte (a)
        mov al, [rdi+rcx]
        ; get str2 byte (b)
        mov bl, [rsi+rcx]
        
        ; if a = 0, return b
        cmp al, 0
        movz al, bl
        jz .ret_diff
        
        ; if b = 0, return -a
        mov r8b, al
        neg r8b
        cmp bl, 0
        movz al, r8b
        jz .ret_diff
        
        ; if a-b != 0, return a-b
        sub al, bl
        cmp al, 0
        jnz .ret_diff
        
        inc rcx ; inc count
        jmp .top
    
    .ret_same:
        ; zero return val
        xor eax, eax
        ret
    
    .ret_diff:
        ; sign extend difference to 32-bit
        cbw
        cwde
        ret
        
; int strncmp(const char *str1, const char *str2, size_t num); // ret >0 -> str1 > str2
strncmp:
    ; for(int count = 0; count < num; ++count)
    xor rcx, rcx ; zero count
    jmp .aft
    .top:
        ; get str1 byte (a)
        mov al, [rdi+rcx]
        ; get str2 byte (b)
        mov bl, [rsi+rcx]
        
        ; if a = 0, return b
        cmp al, 0
        movz al, bl
        jz .ret_diff
        
        ; if b = 0, return -a
        mov r8b, al
        neg r8b
        cmp bl, 0
        movz al, r8b
        jz .ret_diff
        
        ; if a-b != 0, return a-b
        sub al, bl
        cmp al, 0
        jnz .ret_diff
        
        inc rcx ; inc count
    .aft:
        cmp rcx, rdx
        jb .top
    
    .ret_same:
        ; zero return val
        xor eax, eax
        ret
    
    .ret_diff:
        ; sign extend difference to 32-bit
        cbw
        cwde
        ret 
        
; void *memchr (void *ptr, int value, size_t num);
memchr:
    ; for(int i = 0; i < num; ++i)
    xor rcx, rcx
    jmp .aft
    .top:
        ; if this byte is the value, return ptr
        cmp [rdi], sil
        move rax, rdi
        je .ret
        
        inc rcx
    .aft:
        cmp rcx, rdx
        jb .top
        
    xor rax, rax ; broke out of loop - not found (return null)
    .ret: ret
        
; char *strchr (char *str, int character);
strchr:
    .top:
        ; get a character
        mov al, [rdi]
        
        ; if it's the value, return str
        cmp al, sil
        move rax, rdi
        je .ret
        
        ; if it's a null, we're done searching
        cmp al, 0
        je .ret_null
        
        ; inc str and on to next char
        inc rdi
        jmp .top
        
    .ret_null: xor rax, rax
    .ret: ret
        
        
        
        
        
        
        
        
        







   
    
; void *memset(void *ptr, int value, size_t num);
memset:
    mov rax, rdi ; copy ptr to rax (for return value)
    
    jmp .aft
    .top:
        ; copy a byte (value as unsigned char)
        mov [rdi], sil
        
        dec rdx ; dec num
    .aft:
        cmp rdx, 0 ; if num > 0, repeat
        ja .aft
    
; const char *strerror(int errnum);
strerror:
    ; return PLACEHOLDER
    mov rax, errno_NOTIMPLEMENTED
    ret
    
; size_t strlen(const char *str);
strlen:
    xor rax, rax ; zero len
    
    .top:
        ; if this char is null, break
        cmp byte ptr [rdi+rax], 0
        je .ret
        
        ; inc len and repeat
        inc rax
        jmp .top
        
    ; return len
    .ret: ret






segment .rodata

errno_NOTIMPLEMENTED: db "ERRNO NOT IMPLEMENTED YET", 0




