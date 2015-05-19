// code to test the bootloader on a PIC32MX150F128B (HypnoLSD module)
#include "BootLoader.h" 

#include <xc.h>   // standard part port definitions
#include <plib.h> // peripherial definitions
#include <stdbool.h>
#include <stdint.h>  // basic types like uint16_t, etc.

#define SYS_CLOCK 48000000L   // system clock speed

// multiply milliseconds by this to get ticks
#define TICKS_PER_MILLISECOND     ((SYS_CLOCK)/2000)

// version stored in nibbles MAJOR.MINOR
#define VERSION  0x10 // version 1.0, May 2015

/* Version history
 * 1.0 - intial release
  */

#define TEXT_SIZE 100 // size of input/output buffer

// place to format messages
char text[TEXT_SIZE];


/**********************
 * Configuration Bits *
 **********************/

//#pragma config UPLLEN   = OFF           // USB PLL Enabled
//#pragma config UPLLIDIV = DIV_3         // USB PLL Input Divider

#pragma config FPLLMUL  = MUL_24        // PLL Multiplier
#pragma config FPLLIDIV = DIV_3         // PLL Input Divider
#pragma config FPLLODIV = DIV_2         // PLL Output Divider
#pragma config FPBDIV   = DIV_1         // Peripheral Clock divisor

#pragma config FWDTEN   = OFF           // Watchdog Timer
#pragma config WDTPS    = PS1           // Watchdog Timer Postscale
#pragma config FCKSM    = CSDCMD        // Clock Switching & Fail Safe Clock Monitor
#pragma config OSCIOFNC = OFF           // CLKO Enable
#pragma config POSCMOD  = HS            // Primary Oscillator
#pragma config IESO     = OFF           // Internal/External Switch-over
#pragma config FSOSCEN  = OFF           // Secondary Oscillator Enable (KLO was off)
#pragma config FNOSC    = PRIPLL        // Oscillator Selection
#pragma config CP       = ON            // Code Protect
#pragma config BWP      = OFF           // Boot Flash Write Protect
#pragma config PWP      = OFF           // Program Flash Write Protect
#pragma config ICESEL   = ICS_PGx2      // ICE/ICD Comm Channel Select
#pragma config DEBUG    = OFF           // Background Debugger Enable
    // todo - turn on watchdog timer? tickle


/*********************** UART code ********************************************/

// select which to use
#if 1
#define HC_UART1
#else
#define HC_UART2
#endif

//The desired startup baudRate
#define DESIRED_BAUDRATE   (1000000)
UINT16 clockDivider = SYS_CLOCK/(4*DESIRED_BAUDRATE) - 1;

// if there is any UART error, return 1, else return 0
int UARTError()
{
        int err =
        U1STAbits.OERR | // overrun error
        U1STAbits.FERR | // framing error
        U1STAbits.PERR ; // parity error

        err |=
        U2STAbits.OERR | // overrun error
        U2STAbits.FERR | // framing error
        U2STAbits.PERR ; // parity error

        return err!=0?1:0;
}


// read byte if one is ready.
// if exists, return true and byte
// if return false, byte = 0 is none avail, else
// byte != 0 means error
bool UARTReadByte(uint8_t * byte)
    {
    if (UARTReceivedDataIsAvailable(UART1))
    {
        *byte = UARTGetDataByte(UART1);
        return true;
    }
    *byte = 0;
    return false;
    } // UARTReadByte


// blocking call to write byte to UART
void UARTWriteByteMain(char byte)
{
#ifdef HC_UART1
        while (!U1STAbits.TRMT);   // wait till bit clear, then ready to transmit
        U1TXREG = byte; // write a byte
#else
        while (!U2STAbits.TRMT);   // wait till bit clear, then ready to transmit
        U2TXREG = byte; // write a byte
#endif
}

// print the message to the serial port.
void PrintSerialMain(const char * message)
{
    while (*message != 0)
    {
        UARTWriteByteMain(*message);
        message++;
    }
}

// set the baud rate divider
// The baud is Floor[80000000/(4*(divider+1)].
void SetUARTClockDivider(UINT16 divider)
{

	// define setup Configuration 1 for OpenUARTx
		// Module Enable
		// Work in IDLE mode
		// Communication through usual pins
		// Disable wake-up
		// Loop back disabled
		// Input to Capture module from ICx pin
		// no parity 8 bit
		// 1 stop bit
		// IRDA encoder and decoder disabled
		// CTS and RTS pins are disabled
		// UxRX idle state is '1'
		// 4x baud clock - high speed
	#define config1 UART_EN | UART_IDLE_CON | UART_RX_TX | UART_DIS_WAKE | UART_DIS_LOOPBACK | UART_DIS_ABAUD | UART_NO_PAR_8BIT | UART_1STOPBIT | UART_IRDA_DIS | UART_DIS_BCLK_CTS_RTS| UART_NORMAL_RX | UART_BRGH_FOUR

    // define setup Configuration 2 for OpenUARTx
		// IrDA encoded UxTX idle state is '0'
		// Enable UxRX pin
		// Enable UxTX pin
		// No interrupt on transfer of every character to TSR
		// Interrupt on every char received
		// Disable 9-bit address detect
		// Rx Buffer Over run status bit clear
	#define config2 UART_TX_PIN_LOW | UART_RX_ENABLE | UART_TX_ENABLE | /*UART_INT_TX |  UART_INT_RX_CHAR | */ UART_ADR_DETECT_DIS | UART_RX_OVERRUN_CLEAR


#ifdef HC_UART1
    // Open UART1 with config1 and config2
    OpenUART1( config1, config2, divider);

    // Configure UART RX Interrupt
    //ConfigIntUART1(UART_INT_PR2 | UART_RX_INT_EN /* | UART_TX_INT_EN */ );
#else
    // Open UART2 with config1 and config2
    OpenUART2( config1, config2, divider);

    // Configure UART RX Interrupt
    //ConfigIntUART2(UART_INT_PR2 | UART_RX_INT_EN /* | UART_TX_INT_EN */ );
#endif
    clockDivider = divider;
}

// init the UART settings
void InitializeUART(int clock)
{

     clockDivider = clock/(4*DESIRED_BAUDRATE) - 1;

     // UART1
     // U1RX on RPA4
     // U1TX on RPA0
     mPORTAClearBits(BIT_0 | BIT_4);
     mPORTASetPinsDigitalIn(BIT_4);
     mPORTASetPinsDigitalOut(BIT_0);
     U1RXR = 2; // RPB2 = U1RX
     RPA0R = 1; // PIN RPA0 = U1TX

    SetUARTClockDivider(clockDivider);
}


/*********************** main code ********************************************/

// initialize hardware
void Initialize()
{
        // All of these items will affect the performance of your code and cause it to run significantly slower than you would expect.
	SYSTEMConfigPerformance(SYS_CLOCK);

	WriteCoreTimer(0); // Core timer ticks once every two clocks (verified)

	// set default digital port A for IO
	DDPCONbits.JTAGEN = 0; // turn off JTAG
	DDPCONbits.TROEN = 0; // ensure no tracing on
	//mPORTASetPinsDigitalOut(BIT_0 | BIT_1 | BIT_2 | BIT_3 | BIT_4 | BIT_5 | BIT_6 | BIT_7);

        // todo - set this based on width of image. make rest inputs?
        mPORTBSetPinsDigitalOut(BIT_0|BIT_1|BIT_2|BIT_3|BIT_4|BIT_5|BIT_6|BIT_7|BIT_8|BIT_9|BIT_10|BIT_11|BIT_12|BIT_13|BIT_14|BIT_15);

        // Configure the device for maximum performance but do not change the PBDIV
        // Given the options, this function will change the flash wait states, RAM
        // wait state and enable prefetch cache but will not change the PBDIV.
        // The PBDIV value is already set via the pragma FPBDIV option above..
        int pbClk = SYSTEMConfig( SYS_CLOCK, SYS_CFG_WAIT_STATES | SYS_CFG_PCACHE);

        InitializeUART(pbClk);

        mPORTASetPinsDigitalOut(BIT_1);


        // set internals
        // SetUARTClockDivider(flashOptions.baudDivisor);

	// prepare 32 bit timer 45 to trigger interrupts
        //OpenTimer45(T45_ON | T45_SOURCE_INT | T45_PS_1_1, interruptTime);

        // set up the timer interrupt and priority
	//ConfigIntTimer45(T4_INT_ON | T4_INT_PRIOR_7);

        // enable multivectored interrupts
	//INTEnableSystemMultiVectoredInt();

	// start watchdog timer
	//tickle in interrupt, turn off during reset of device, causes a reset
	//The next statement enables the Watchdog Timer:
	// WDTCONbits.ON = 1;

        //sprintf(text,"Wait states %d\r\n.",BMXCONbits.BMXWSDRM);
        //PrintSerial(text);
        //BMXCONbits.BMXWSDRM = 0; // set RAM access to zero wait states

        //sprintf(text,"TODO _ REMOVE:override %d\r\n",pinOverride);
        //PrintSerial(text);
        //sprintf(text,"TODO _ REMOVE:actual %d\r\n",PORTAbits.RA1);
        //PrintSerial(text);
}

// wait the given number of milliseconds
void DelayMs(int milliseconds)
{
    UINT32 time = ReadCoreTimer();
    UINT32 startTicks = milliseconds*TICKS_PER_MILLISECOND;
    UINT32 ticks = startTicks-1;

    // loop till rolls over
    while (ticks <= startTicks)
    {
        UINT32 newTime = ReadCoreTimer();
        ticks -= newTime-time;
        time = newTime;
    }
}

int main(void)
{
    // BootloaderEntry();

    Initialize();

    sprintf(text,"\r\n\r\nHypnocube Boot Loader testing ver %d.%d.\r\n",VERSION>>4, VERSION&15);
    PrintSerialMain(text);

    sprintf(text,"Boot loader result %d.\r\n",(int)bootResult);
    PrintSerialMain(text);
    
    WriteCoreTimer(0);
    while (1)
    {
        //DelayMs(1000);
        if (ReadCoreTimer()>1000*TICKS_PER_MILLISECOND)
        {
            PrintSerialMain(".");
            WriteCoreTimer(0);
            PORTAbits.RA1^=1; // blink our LED
        }
        uint8_t byte;
        
        if (UARTReadByte(&byte) && byte != 0xFC)
        {
            sprintf(text,"Main saw command %d = %c.\r\n",(int)byte,byte);
            PrintSerialMain(text);
            BootloaderEntry(); // call again to simplify testing
        }
    }

    return 0;
}


