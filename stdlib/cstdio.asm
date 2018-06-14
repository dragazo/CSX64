global FILE.sz

global remove
global rename

; --------------------------------------

; struct FILE
;     int fd; (-1 for none)
;     int bpos; (pos for reading, len for writing)
;     char buffer[1016];
FILE:
    .fd: equ 0
    .bpos: equ 4
    .buffer: equ 8
    
    .bcap: equ 1016
    .sz: equ 1024

; --------------------------------------
    
segment .text

; int remove(const char *path);
remove:
    ; delete the file
    mov eax, sys_remove
    mov rbx, rdi
    syscall
    
    ; return 0 (failure causes CSX64 to terminate)
    xor eax, eax
    ret

; int rename(const char *from, const char *to);
rename:
    ; rename the file
    mov eax, sys_move
    mov rbx, rdi
    mov rcx, rsi
    syscall
    
    ; return 0 (failure causes CSX64 to terminate)
    xor eax, eax
    ret
    






; FILE *fopen(const char *path, const char *mode)
fopen:
    xor eax, eax ; eax is file mode (0 invalid)
    xor ebx, ebx ; ebx is file access (0 invalid)
    
    .mode_top:
        mov dl, [rsi] ; get the mode character
        cmp dl, 0
        je .mode_end ; null term is end of mode string
        
        cmp dl, 'r'
        
    .mode_end:
        


segment .rodata

fopen_readd


