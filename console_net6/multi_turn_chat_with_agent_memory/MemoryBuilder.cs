using LMKit.Agents;

namespace multi_turn_chat_with_agent_memory
{
    internal class MemoryBuilder
    {
        public static async Task<AgentMemory> Generate()
        {
            const string fileName = "memory.bin";

            if (File.Exists(fileName))
            {
                return AgentMemory.Deserialize("memory.bin", LMKit.Model.LM.LoadFromModelID("nomic-embed-text"));
            }

            var memory = new AgentMemory(LMKit.Model.LM.LoadFromModelID("nomic-embed-text"));

            var acmeeProfileCollection = "acmeeCustomerProfile";

            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact1", text: "What is the ideal customer size (number of employees)? -> Between 200 and 500 employees.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact2", text: "What is the typical annual revenue of the ideal customer? -> $20 million to $200 million.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact3", text: "In which industries are Acmee’s ideal customers primarily found? -> High-tech sectors such as software development, IT services, and digital media.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact4", text: "What key challenge drives these organizations to pursue digital transformation? -> Their recognition of legacy system limitations and the need for integrated digital platforms.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact5", text: "Who are the key decision-makers in these organizations? -> Forward-thinking executives like CTOs and CIOs who prioritize cloud computing and real-time analytics.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact6", text: "Where are Acmee’s ideal customers primarily located? -> Major metropolitan hubs in North America and Western Europe, with a growing presence in Asia.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact7", text: "What IT infrastructure investments have these organizations made? -> Transitioning to cloud-centric operations fortified with robust cybersecurity and scalable systems.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact8", text: "What is a key operational priority for these companies? -> Achieving rapid yet deliberate digital transformation by integrating AI and automation.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact9", text: "How do these organizations demonstrate digital maturity? -> By investing in process automation, advanced data analytics, and comprehensive system integration.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact10", text: "What challenge do these organizations face regarding their IT systems? -> Harmonizing disparate systems across departments and bridging technological silos.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact11", text: "How are vendor relationships structured for these organizations? -> They build long-term strategic partnerships that emphasize continuous innovation, collaborative problem-solving, and proactive support.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact12", text: "How do these organizations manage risk? -> By balancing investments in emerging technologies with rigorous risk management practices.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact13", text: "What is the typical annual technology budget for these organizations? -> Between $500,000 and $5 million.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact14", text: "What is a non-negotiable requirement for the digital solutions these companies seek? -> Scalability that allows solutions to evolve with expanding operations and adapt to dynamic market demands.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact15", text: "How important is environmental sustainability to these organizations? -> It is integral, driving them to favor solutions that enhance efficiency while reducing their carbon footprint.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact16", text: "How is success measured in these organizations? -> Through a holistic approach that combines quantitative metrics (like efficiency and time-to-market) with qualitative outcomes (such as customer satisfaction and brand loyalty).");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact17", text: "What is a critical technical requirement for Acmee's ideal customer? -> The ability to seamlessly integrate new digital platforms with existing systems like ERP, CRM, and business intelligence tools.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact18", text: "How is innovation embedded in these organizations? -> They consistently experiment with emerging technologies and adopt methodologies that provide a sustainable competitive edge.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact19", text: "What role does customization play for these companies? -> It is a strategic necessity, with highly tailored digital solutions required to address unique operational challenges.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact20", text: "Why are data security and compliance critical for these organizations? -> Stringent regulatory requirements drive them to invest in advanced cybersecurity measures that protect sensitive data and ensure compliance.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact21", text: "What are the customer support expectations for these organizations? -> They expect 24/7 dedicated service along with proactive account management to swiftly resolve issues.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact22", text: "What revenue model do many of Acmee's ideal customers follow? -> Subscription-based or recurring revenue models, which require continuous evolution of digital solutions.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact23", text: "How do these organizations approach the evaluation and selection of technology vendors? -> Through a methodical, comprehensive process that spans several months to ensure full stakeholder engagement and strategic alignment.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact24", text: "What characterizes internal communication within these organizations? -> A data-driven approach emphasizing transparency, regular performance reviews, and strategic insights.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact25", text: "What distinguishes Acmee’s ideal customer? -> A unique blend of technological ambition, financial stability, and a relentless pursuit of innovation that fosters transformative digital solutions.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact26", text: "What is the average timeline for a complete digital transformation? -> Typically, 12 to 18 months.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact27", text: "What is the typical size of an internal IT team? -> Generally, between 20 and 100 dedicated IT professionals.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact28", text: "What are common strategic objectives for these organizations? -> Enhancing operational efficiency, boosting customer engagement, and driving revenue growth.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact29", text: "How do these organizations foster digital innovation? -> By cultivating a culture of continuous improvement and encouraging cross-functional collaboration.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact30", text: "What portion of the budget is usually allocated to technology investments? -> Around 10-20% of the overall budget.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact31", text: "What procurement method is preferred for technology solutions? -> A blend of in-house development and strategic vendor partnerships.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact32", text: "How are project success metrics typically defined? -> Through key performance indicators such as ROI, customer satisfaction, and process optimization.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact33", text: "What role does data analytics play in these organizations? -> It is central to decision-making and performance optimization.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact34", text: "How do these companies address cybersecurity threats? -> By implementing multi-layered defense strategies and continuous monitoring.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact35", text: "What common challenge arises when integrating new technology? -> Ensuring compatibility with legacy systems while minimizing operational disruptions.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact36", text: "How is digital adoption success measured? -> Through metrics like user engagement and overall system utilization.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact37", text: "What is the impact of digital transformation on operational efficiency? -> It streamlines workflows and significantly reduces time-to-market.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact38", text: "How is change management typically handled? -> Through comprehensive training programs and effective communication strategies.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact39", text: "What role does cloud computing play in their IT strategy? -> It provides a foundation for scalability, flexibility, and cost-efficiency.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact40", text: "How do these organizations ensure regulatory compliance? -> By integrating compliance checks into digital platforms and updating protocols regularly.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact41", text: "What is the typical approach to data management? -> Utilizing centralized data warehouses combined with advanced analytics tools.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact42", text: "How important is mobile accessibility for these companies? -> Extremely important for supporting remote work and on-the-go decision-making.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact43", text: "What strategy is used for customer data integration? -> Leveraging CRM systems to unify data from multiple customer touchpoints.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact44", text: "How are innovation investments prioritized? -> Based on alignment with strategic goals and potential return on investment.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact45", text: "What is the expected ROI timeframe for digital transformation initiatives? -> Typically between 18 to 36 months.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact46", text: "How do these organizations develop digital skills internally? -> Through targeted training programs, hiring specialized talent, and partnering with technology experts.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact47", text: "What are common barriers to successful digital transformation? -> Budget constraints, outdated legacy systems, and resistance to change.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact48", text: "How is social media integrated into their digital strategy? -> As a key tool for customer engagement and targeted marketing efforts.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact49", text: "What role does artificial intelligence play in these organizations? -> It automates processes, enhances decision-making, and personalizes customer experiences.");
            await memory.SaveInformationAsync(acmeeProfileCollection, id: "fact50", text: "How do these companies view the future of digital technology? -> As a critical driver of growth, innovation, and competitive differentiation.");

            memory.Serialize(fileName);

            return memory;
        }
    }
}
