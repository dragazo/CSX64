; source http://www.cplusplus.com/reference/cerrno/
; needs the specific error numbers plus messages in strerror() in string.asm.
; also needs to be integrated into the relevant stdlib functions.
global errno

segment .bss

align 4
errno: resd 1
