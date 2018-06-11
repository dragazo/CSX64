global main ; the linker requires a global named main

segment .text

main: ; a label marking the beginning of main function
    mov eax, sys_write ; load a system call code into eax
    mov ebx, 1         ; load file descriptor into ebx (fd 1 is stdout)
    mov ecx, txt       ; move the value of txt (an address) into ecx
    mov edx, txt_len   ; move the length of txt into edx
    syscall            ; perform the system call (writes the string)
    
    xor eax, eax       ; zero return value (using the assembly xor idiom)
    ret                ; return 0 to caller (in this case the OS)

segment .rodata

; create a label for txt and declare a string and a new line character
txt: db "Hello World!", 10
; define a new symbol txt_len to be the length of the string
txt_len: equ $-txt ; (uses $ current line macro)
