# Enterprise-Grade .NET SDK for Integrating Generative AI Capabilities. | Demo repository

> **With LM-Kit.NET, integrating or building AI is no longer complex.**  

**LM-Kit.NET** is a cutting-edge, cross-platform SDK that offers a wide range of advanced **Generative AI** capabilities.  
It enables seamless orchestration of multiple AI models through a **single API**, tailored to meet specific business needs.  
The SDK offers cutting-edge AI capabilities across a wide range of domains, including [text completion](https://www.easyllm.tech/docs/text-completion.html), chat assistance, content retrieval, text analysis, translation, and more...

 

## **Wide range of capabilities**

LM-Kit.NET offers a suite of highly optimized low-level APIs designed to facilitate the development of fully customized Large Language Model (LLM) inference pipelines.  

Additionally, LM-Kit.NET provides an extensive array of high-level AI functionalities spanning multiple domains, including:  

- **Text Generation:** Create coherent and contextually relevant text automatically.
- **Text Quality Evaluation:** Assess the quality metrics of generated text content.
- **Language Detection:** Identify the language of text input with high accuracy.
- **Text Translation:** Convert text between multiple languages seamlessly.
- **Text Correction:** Correct grammar and spelling in text of any length.
- **Text Rewriting:** Rewrite text using a specific communication style.
- **Code Analysis:** Perform various programming code processing tasks.
- **Model Fine-Tuning:** Customize pre-trained models to better suit specific needs.
- **Model Quantization:** Optimize models for efficient inference.
- **Retrieval-Augmented Generation (RAG):** Enhance text generation with information retrieved from a large corpus.
- **Text Embeddings:** Transform text into numerical representations that capture semantic meanings.
- **Question Answering:** Provide answers to queries, supporting both single-turn and multi-turn interactions.
- **Custom Text Classification:** Categorize text into predefined classes according to content.
- **Sentiment Analysis:** Detect and interpret the emotional tone from text.
- **Emotion Detection:** Identify specific emotions expressed in text.
- **Sarcasm Detection:** Detect instances of sarcasm in written text.
- **And More:** Explore additional features that extend the capabilities of your applications.
 

These ever-expanding capabilities ensure seamless integration of advanced AI solutions, tailored to meet diverse needs through a single Software Development Kit (SDK). 

  

## **Run local LLMs on any device**

The LM-Kit.NET model inference system is powered by [llama.cpp](https://github.com/ggerganov/llama.cpp), which delivers state-of-the-art performance across a broad range of hardware with minimal setup and no dependencies.  
LM-Kit.NET exclusively performs inference on-device (also known as edge computing), enabling full control and precise tuning of the inference process.  
Additionally, LM-Kit.NET supports an ever-expanding range of model architectures, including LLaMA-2, LLaMA-3, Mistral, Falcon, Phi, and more.  

 

## **Highest degree of performance**

### 1. Optimized for various GPUs and CPUs
LM-Kit.NET is expertly engineered to maximize the capabilities of various hardware configurations, ensuring top-tier performance across all platforms.  
This multi-platform optimization allows LM-Kit.NET to specifically leverage the unique hardware strengths of each device. For instance, it automatically uses **CUDA** on NVIDIA GPUs to boost computation speeds significantly, and **Metal** on Apple devices to enhance both graphics and processing tasks.  

### 2. State of the art architectural foundations
The core system of LM-Kit.NET has undergone rigorous optimization to handle a wide array of scenarios efficiently.  
Its advanced internal caching and recycling mechanisms are designed to maintain high performance levels consistently, even under varied operational conditions.  
Whether your application is running a single instance or multiple concurrent instances, LM-Kit.NET's sophisticated core system orchestrates all requests smoothly, delivering rapid performance while minimizing resource consumption.

### 3. Unrivaled performances
Experience model inference speeds up to 5x faster with LM-Kit.NET, thanks to its cutting-edge underlying technologies that are continuously refined and benchmarked to ensure you stay ahead of the curve.

 

## **Be an Early Adopter of the latest and future Generative AI innovations**

LM-Kit.NET is crafted by industry experts employing a strategy of **continuous innovation**.  
It is designed to rapidly address emerging market needs and introduce new capabilities to modernize existing applications.   
Leveraging state-of-the-art AI technologies, LM-Kit.NET offers a modern, user-friendly, and intuitive API suite, making advanced AI accessible for any type of application.

  

## **Maintain full control over your data**

Maintaining full control over your data is crucial for both privacy and security.  
By using LM-Kit.NET, which performs model inference directly on-device, you ensure that your sensitive data remains within your controlled environment and does not traverse external networks. 
Here are some key benefits of this approach:

### 1. Enhanced Privacy
Since all data processing is done locally on your device, there is no need to send data to a remote server.  
This drastically reduces the risk of exposure or leakage of sensitive information, keeping your data confidential.

### 2. Increased Security
With zero external requests, the risk of intercepting data during transmission is completely eliminated.  
This closed system approach minimizes vulnerabilities that are often exploited in data breaches, offering a more secure solution.

### 3. Faster Response Times
Processing data locally reduces the latency typically associated with sending data to a remote server and waiting for a response.  
This results in quicker model inferences, leading to faster decision-making and improved user experience.

### 4. Reduced Bandwidth Usage
By avoiding the need to transfer large volumes of data over the internet, LM-Kit.NET minimizes bandwidth consumption.  
This is particularly beneficial in environments with limited or costly data connectivity.

### 5. Full Compliance with Data Regulations
Local processing helps in complying with strict data protection regulations, such as GDPR or HIPAA, which often require certain types of data to be stored and processed within specific geographical boundaries or environments.
By leveraging LM-Kit.NET on-device processing capabilities, organizations can achieve higher levels of data autonomy and protection, while still benefiting from advanced computational models and real-time analytics.

 

## **Seamless integration and simple deployment**

LM-Kit.NET offers an exceptionally streamlined deployment model, being packaged as a single NuGet for all supported platforms.  
Integrating LM-Kit.NET into any .NET application is a straightforward process, typically requiring just a few clicks.
LM-Kit.NET combines C# and C++ coding, meticulously crafted without dependencies to perfectly suit its functionalities.  

### 1. Simplified Integration
LM-Kit.NET requires no external containers or complex deployment procedures, making the integration process exceptionally straightforward.  
This approach significantly reduces development time and lowers the learning curve, enabling a broader range of developers to effectively deploy and leverage the technology.

### 2. Streamlined Deployment
LM-Kit.NET is designed for efficiency and simplicity. By default, it runs directly within the same application process that calls it, avoiding the complexities and resource demands typically associated with containerized systems.  
This direct integration accelerates performance and simplifies the incorporation into existing applications by removing the common hurdles associated with container use.

### 3. Efficient Resource Management
Operating in-process, LM-Kit.NET minimizes its impact on system resources, making it ideal for devices with limited capacity or situations where maximizing computing efficiency is essential.

### 4. Enhanced Reliability
By avoiding reliance on external services or containers, LM-Kit.NET offers more stable and predictable performance.  
This reliability is vital for applications that demand consistent, rapid data processing without external dependencies.

 

## **Supported Operating Systems**

LMkit.NET is designed for full compatibility with a wide range of operating systems, ensuring smooth and reliable performance on all supported platforms:

- Windows: Compatible with versions from Windows 7 through to the latest release.
- macOS: Supports macOS 11 and all subsequent versions.
- Linux: Functions optimally on distributions with glibc version 2.27 or newer.

 

## **Supported .NET Frameworks**

LMkit.NET is compatible with a wide range of .NET frameworks, spanning from version 4.6.2 up to .NET 8.  
To maximize performance through specific optimizations, separate binaries are provided for each supported framework version.  
Accordingly, the NuGet package includes assemblies targeting:
- netstandard2.0
- net5.0
- net6.0
- net7.0
- net8.0
